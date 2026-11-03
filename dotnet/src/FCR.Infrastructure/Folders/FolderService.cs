using FCR.Application;
using FCR.Domain;
using Microsoft.Extensions.Options;

namespace FCR.Infrastructure.Folders;

/// <summary>
/// Folder resolution — parity with fcr_code.py's generate_folder_path /
/// find_existing_folder. Directory checks hit the configured attachment_root.
/// </summary>
public sealed class FolderService(ISettingsStore settings, IOptions<FcrOptions> options) : IFolderService
{
    public string AttachmentRoot => string.IsNullOrWhiteSpace(options.Value.AttachmentRoot)
        ? settings.Load().AttachmentRoot
        : options.Value.AttachmentRoot;

    public string GenerateFolderPath(string? fcrCode) =>
        FcrCode.GenerateFolderPath(AttachmentRoot, fcrCode);

    public string ResolveFolderPath(string? fcrCode)
    {
        if (string.IsNullOrWhiteSpace(fcrCode)) return "";
        var (area, dept) = FcrCode.Parse(fcrCode);
        if (area.Length == 0 || dept.Length == 0) return "";

        var basePath = Path.Combine(AttachmentRoot, area, dept);
        try
        {
            if (Directory.Exists(basePath))
            {
                var code = fcrCode.Trim();
                var match = Directory.EnumerateDirectories(basePath)
                    .FirstOrDefault(d => Path.GetFileName(d).StartsWith(code));
                if (match is not null) return match;
            }
        }
        catch { /* unreadable share → fall through to derived path */ }
        return Path.Combine(AttachmentRoot, area, dept, fcrCode.Trim());
    }
}
