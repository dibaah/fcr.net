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

        return Path.GetFullPath(folder);
    }
}
