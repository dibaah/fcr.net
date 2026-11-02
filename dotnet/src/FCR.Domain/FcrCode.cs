using System.Text.RegularExpressions;

namespace FCR.Domain;

/// <summary>Domain-level validation failure. Maps to HTTP 400 in the Web layer.</summary>
public class FcrValidationException(string message) : Exception(message);

/// <summary>FCR code rules — parity with app/services/fcr_code.py.</summary>
public static partial class FcrCode
{
    [GeneratedRegex(FcrConstants.FcrCodePattern)]
    private static partial Regex CodeRegex();

    /// <summary>Trim, upper-case, strip internal whitespace.</summary>
    public static string Normalize(object? value) =>
        Regex.Replace((value?.ToString() ?? ""), @"\s+", "").ToUpperInvariant();

    /// <summary>Normalized code or "" when blank. Throws <see cref="FcrValidationException"/> when malformed.</summary>
    public static string Validate(object? value)
    {
        var code = Normalize(value);
        if (code.Length == 0) return "";
        var match = CodeRegex().Match(code);
        if (!match.Success)
            throw new FcrValidationException(
                $"Invalid FCR Code '{code}'. Expected format FCR-<AREA>-<DEPT>-<NNN>, e.g. FCR-CT-PI-046.");
        var (area, dept) = (match.Groups[1].Value, match.Groups[2].Value);
        if (!FcrConstants.AreaFolderMap.ContainsKey(area))
            throw new FcrValidationException(
                $"Unknown area code '{area}' in FCR Code '{code}'. Allowed: {string.Join(", ", FcrConstants.AreaFolderMap.Keys.Order())}.");
        if (!FcrConstants.DefaultDeptCodes.ContainsKey(dept))
            throw new FcrValidationException(
                $"Unknown department code '{dept}' in FCR Code '{code}'. Allowed: {string.Join(", ", FcrConstants.DefaultDeptCodes.Keys.Order())}.");
        return code;
    }

    /// <summary>'FCR-CT-PI-046' → ('CTEP', 'PI'); ('','') when unparseable.</summary>
    public static (string AreaFolder, string Dept) Parse(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return ("", "");
        var parts = code.Trim().Split('-', '_');
        var areaAbbrev = parts.Length > 1 ? parts[1].ToUpperInvariant() : "";
        var deptAbbrev = parts.Length > 2 ? parts[2].ToUpperInvariant() : "";
        return (FcrConstants.AreaFolderMap.GetValueOrDefault(areaAbbrev, ""), deptAbbrev);
    }

    /// <summary>&lt;parent&gt;/&lt;area&gt;/&lt;dept&gt;/&lt;code&gt; or "" when the code doesn't map.</summary>
    public static string GenerateFolderPath(string parentPath, string? fcrCode)
    {
        var (area, dept) = Parse(fcrCode);
        if (area.Length == 0 || dept.Length == 0) return "";
        return Path.Combine(parentPath, area, dept, fcrCode!.Trim());
    }
}
