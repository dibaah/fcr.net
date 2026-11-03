using FCR.Domain;
using Microsoft.Extensions.Options;

namespace FCR.Infrastructure.Attachments;

/// <summary>
/// Centralized attachment storage for FCR requests (master plan §3.1/§7):
/// files under {RequestAttachmentDir}/{requestId}/{guid}_{safe-name}, with
/// size/type validation and path-traversal protection.
/// </summary>
public sealed class AttachmentStore(IOptions<FcrOptions> options)
{
    public const long MaxFileBytes = 25L * 1024 * 1024; // 25 MB

    private static readonly HashSet<string> DeniedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".msi", ".ps1", ".com", ".scr", ".dll", ".vbs", ".sh", ".jar",
    };

    public string RootDir =>
        string.IsNullOrWhiteSpace(options.Value.RequestAttachmentDir)
            ? Path.Combine(options.Value.DataDir, "request-attachments")
            : options.Value.RequestAttachmentDir;

    public static string SanitizeFileName(string? name)
    {
        var file = Path.GetFileName((name ?? "").Replace('\\', '/'));
        return file.Length is 0 or > 200 ? "" : file;
    }

    public async Task<string> SaveAsync(string requestId, string originalFileName,
        Stream content, CancellationToken ct)
    {
        var safe = SanitizeFileName(originalFileName);
        if (safe.Length == 0)
            throw new FcrValidationException("Invalid file name");
        var ext = Path.GetExtension(safe);
        if (DeniedExtensions.Contains(ext))
            throw new FcrValidationException($"File type '{ext}' is not allowed");

        var dir = Path.Combine(RootDir, requestId);
        Directory.CreateDirectory(dir);
        var stored = $"{Guid.NewGuid():N}_{safe}";
        var path = Path.Combine(dir, stored);
        try
        {
            await using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            var buffer = new byte[81920];
            long total = 0;
            int n;
            while ((n = await content.ReadAsync(buffer, ct)) > 0)
            {
                total += n;
                if (total > MaxFileBytes)
                    throw new FcrValidationException("File exceeds the 25 MB limit");
                await fs.WriteAsync(buffer.AsMemory(0, n), ct);
            }
        }
        catch
        {
            // Remove partially written content on size limit, cancellation, or I/O failure.
            try { File.Delete(path); } catch (IOException) { }
            throw;
        }
        return Path.Combine(requestId, stored);
    }

    /// <summary>Resolves a stored relative path inside the attachment root; rejects traversal.</summary>
    public string Resolve(string relativeStoredName)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(RootDir));
        var full = Path.GetFullPath(Path.Combine(root, relativeStoredName));
        var rootPrefix = root + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!full.StartsWith(rootPrefix, comparison))
            throw new FcrValidationException("Invalid attachment path");
        return full;
    }

    public void DeleteFile(string relativeStoredName)
    {
        try { File.Delete(Resolve(relativeStoredName)); } catch (IOException) { }
    }
}
