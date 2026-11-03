using System.Net;
using System.Text;
using System.Text.Json;
using FCR.Application;
using FCR.Domain;
using FCR.Domain.Entities;
using FCR.Infrastructure.Attachments;
using FCR.Infrastructure.Folders;

namespace FCR.Infrastructure.Archive;

/// <summary>
/// Idempotently mirrors a submitted request and its attachments into the official
/// FCR-number archive. Original request files remain in the module store.
/// </summary>
public sealed class FcrArchiveService(AttachmentStore attachmentStore, IFolderService folders)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<string> ArchiveAsync(
        FcrRequest request, string requesterUsername,
        IReadOnlyCollection<FcrAttachment> attachments,
        CancellationToken ct = default)
    {
        var code = FcrCode.Validate(request.FcrCode);
        if (code.Length == 0)
            throw new FcrValidationException("Assign an FCR Code before archiving the request.");

        var folder = folders.ResolveFolderPath(code);
        if (string.IsNullOrWhiteSpace(folder))
            folder = folders.GenerateFolderPath(code);
        if (string.IsNullOrWhiteSpace(folder))
            throw new FcrValidationException($"Unable to resolve archive folder for FCR '{code}'.");

        Directory.CreateDirectory(folder);
        var archived = new List<object>();
        var archivedFiles = new List<(long Id, string Name)>();

        foreach (var attachment in attachments.OrderBy(a => a.Id))
        {
            ct.ThrowIfCancellationRequested();
            var source = attachmentStore.Resolve(attachment.StorageLocation);
            if (!File.Exists(source))
                throw new FileNotFoundException(
                    $"The original attachment for request '{request.RequestNumber}' is missing.");

            var safeName = AttachmentStore.SanitizeFileName(attachment.OriginalFileName);
            if (safeName.Length == 0)
                throw new FcrValidationException($"Attachment {attachment.Id} has an invalid file name.");

            var archiveName = $"ATT-{attachment.Id:D8}_{safeName}";
            var destination = Path.Combine(folder, archiveName);
            File.Copy(source, destination, overwrite: true);
            archivedFiles.Add((attachment.Id, archiveName));
            archived.Add(new
            {
                attachmentId = attachment.Id,
                fileName = attachment.OriginalFileName,
                archiveFileName = archiveName,
                attachment.ContentType,
                attachment.FileSize,
                attachment.UploadedAt,
                attachment.UploadedByUserId,
            });
        }

        var manifest = new
        {
            archiveSchemaVersion = 1,
            request.Id,
            request.RequestNumber,
            fcrCode = code,
            request.Category,
            request.Contractor,
            request.Subcontractor,
            title = request.Subject,
            request.Justification,
            request.Description,
            requesterUsername,
            request.SubmittedAt,
            request.CreatedAt,
            request.UpdatedAt,
            archivedAtUtc = DateTime.UtcNow,
            attachments = archived,
        };

        var manifestPath = Path.Combine(folder, $"{code}-request.json");
        var temporaryPath = manifestPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(
                temporaryPath, JsonSerializer.Serialize(manifest, JsonOptions), ct);
            File.Move(temporaryPath, manifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        var htmlPath = Path.Combine(folder, $"{code}-request.html");
        await File.WriteAllTextAsync(
            htmlPath, BuildHtml(request, requesterUsername, code, archivedFiles),
            new UTF8Encoding(false), ct);

        return Path.GetFullPath(folder);
    }

    private static string BuildHtml(
        FcrRequest request, string requesterUsername, string fcrCode,
        IReadOnlyList<(long Id, string Name)> attachments)
    {
        static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
        static string Row(string label, string? value) =>
            $"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>";

        var files = attachments.Count == 0
            ? "<p>No attachments.</p>"
            : "<ul>" + string.Join("", attachments.Select(a =>
                $"<li><a href=\"{Uri.EscapeDataString(a.Name)}\">{E(a.Name)}</a></li>")) + "</ul>";

        return $@"<!doctype html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>{E(fcrCode)} — FCR Request</title>
<style>
body{{font:14px/1.5 Arial,sans-serif;max-width:900px;margin:32px auto;padding:0 24px;color:#222}}
h1{{border-bottom:2px solid #333;padding-bottom:8px}}table{{width:100%;border-collapse:collapse}}
th,td{{border:1px solid #ccc;padding:8px;text-align:left;vertical-align:top}}
th{{width:190px;background:#f3f3f3}}.text{{white-space:pre-wrap;overflow-wrap:anywhere}}
@media print{{body{{margin:0;max-width:none}}}}
</style>
</head>
<body>
<h1>FCR Request Archive</h1>
<table>
{Row("FCR Number", fcrCode)}
{Row("Request Number", request.RequestNumber)}
{Row("Title", request.Subject)}
{Row("Category", request.Category)}
{Row("Contractor", request.Contractor)}
{Row("Subcontractor", request.Subcontractor)}
{Row("Requester", requesterUsername)}
{Row("Submitted At", request.SubmittedAt)}
{Row("Request Status", request.SubmissionStatus)}
{Row("Linked FCR Record ID", request.LinkedFcrId?.ToString())}
</table>
<h2>Justification</h2><div class=""text"">{E(request.Justification)}</div>
<h2>Description</h2><div class=""text"">{E(request.Description)}</div>
<h2>Attachments</h2>{files}
</body>
</html>";
    }
}
