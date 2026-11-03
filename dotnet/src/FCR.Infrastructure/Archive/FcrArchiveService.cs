using System.Net;
using System.Text;
using FCR.Application;
using FCR.Domain;
using FCR.Infrastructure.Attachments;
using FCR.Infrastructure.Folders;
using Microsoft.EntityFrameworkCore;

namespace FCR.Infrastructure.Archive;

/// <summary>
/// Writes a printable HTML request form and copies request attachments into the
/// official FCR-number archive folder. Copies are deterministic/idempotent so
/// an admin can safely retry after a partial filesystem failure.
/// </summary>
public sealed class FcrArchiveService(
    FcrDbContext db, AttachmentStore attachmentStore, IFolderService folders)
{
    private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    public async Task<ArchiveInfoDto?> ArchiveAsync(string requestId, CancellationToken ct = default)
    {
        var request = await db.FcrRequests
            .Include(r => r.Requester)
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (request is null) return null;

        var code = FcrCode.Validate(request.FcrCode);
        if (code.Length == 0)
            throw new FcrValidationException("FCR number must be assigned before archiving");
        if (string.IsNullOrWhiteSpace(request.Category))
            throw new FcrValidationException("FCR category must be assigned before archiving");

        var rawPath = folders.GenerateFolderPath(code);
        if (string.IsNullOrWhiteSpace(rawPath))
            throw new FcrValidationException($"Cannot resolve archive folder for FCR '{code}'");
        var archivePath = Path.GetFullPath(rawPath);

        if (!string.IsNullOrWhiteSpace(request.ArchivePath)
            && !Path.GetFullPath(request.ArchivePath).Equals(
                archivePath, StringComparison.OrdinalIgnoreCase))
            throw new FcrValidationException(
                "This request was already archived under a different FCR number; changing the number requires an administrator-led archive correction.");

        Directory.CreateDirectory(archivePath);
        var attachmentsPath = Path.Combine(archivePath, "Attachments");
        Directory.CreateDirectory(attachmentsPath);

        var archivedFiles = new List<(long Id, string Name)>();
        foreach (var attachment in request.Attachments.OrderBy(a => a.Id))
        {
            var source = attachmentStore.Resolve(attachment.StorageLocation);
            if (!File.Exists(source))
                throw new FcrValidationException(
                    $"Attachment '{attachment.OriginalFileName}' is missing from request storage");

            var safeName = AttachmentStore.SanitizeFileName(attachment.OriginalFileName);
            if (safeName.Length == 0) safeName = "attachment";
            var archivedName = $"{attachment.Id:D6}_{safeName}";
            var destination = Path.Combine(attachmentsPath, archivedName);
            File.Copy(source, destination, overwrite: true);
            archivedFiles.Add((attachment.Id, archivedName));
        }

        var archivedAt = Now();
        var formFileName = $"{code}_Request.html";
        var formPath = Path.Combine(archivePath, formFileName);
        var html = BuildFormHtml(request, code, archivedAt, archivedFiles);
        await File.WriteAllTextAsync(formPath, html, new UTF8Encoding(false), ct);

        request.FcrCode = code;
        request.ArchivePath = archivePath;
        request.ArchivedAt = archivedAt;
        request.UpdatedAt = archivedAt;
        await db.SaveChangesAsync(ct);

        return new ArchiveInfoDto(
            request.Id, request.RequestNumber, code, archivePath,
            formFileName, archivedAt, archivedFiles.Count);
    }

    private static string BuildFormHtml(
        FCR.Domain.Entities.FcrRequest request,
        string fcrCode, string archivedAt, IReadOnlyList<(long Id, string Name)> attachments)
    {
        static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
        static string Row(string label, string? value) =>
            $"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>";

        var files = attachments.Count == 0
            ? "<p>No attachments.</p>"
            : "<ul>" + string.Join("", attachments.Select(a =>
                $"<li><a href=\"Attachments/{Uri.EscapeDataString(a.Name)}\">{E(a.Name)}</a></li>")) + "</ul>";

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
{Row("Requester", request.Requester?.Username)}
{Row("Submitted At", request.SubmittedAt)}
{Row("Request Status", request.SubmissionStatus)}
{Row("Linked FCR Record ID", request.LinkedFcrId?.ToString())}
{Row("Archived At", archivedAt)}
</table>
<h2>Justification</h2><div class=""text"">{E(request.Justification)}</div>
<h2>Description</h2><div class=""text"">{E(request.Description)}</div>
<h2>Attachments</h2>{files}
</body>
</html>";
    }
}
