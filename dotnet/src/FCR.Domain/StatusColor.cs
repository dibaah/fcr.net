namespace FCR.Domain;

/// <summary>Row colour for export — parity with get_color() in fcr_code.py.</summary>
public static class StatusColor
{
    public const string Red = "red";
    public const string Green = "green";
    public const string Yellow = "yellow";
    public const string None = "none";

    public static string Get(string? pedecStatus, string? fcrStatus, string? executionStatus)
    {
        var p = (pedecStatus ?? "").ToLowerInvariant();
        var f = (fcrStatus ?? "").ToLowerInvariant();
        var e = (executionStatus ?? "").ToLowerInvariant();
        if (HasAny(f, p, "rejected", "suspended", "cancelled")) return Red;
        if (e.Contains("finished") || f == "closed" || p == "closed") return Green;
        if (HasAny(e, "pending", "awaiting", "on-going", "preparation")) return Yellow;
        if (HasAny(f, "issued", "under evaluation", "expert approved", "commented")) return Yellow;
        return None;
    }

    private static bool HasAny(string text, params string[] keys) =>
        keys.Any(text.Contains);

    private static bool HasAny(string a, string b, params string[] keys) =>
        keys.Any(k => a.Contains(k) || b.Contains(k));
}

/// <summary>parse_date() parity — 4 formats, first 10 chars.</summary>
public static class FcrDate
{
    private static readonly string[] Formats = ["yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "MM/dd/yyyy"];

    public static DateTime? Parse(object? value)
    {
        var s = (value?.ToString() ?? "").Trim();
        if (s.Equals("none", StringComparison.OrdinalIgnoreCase)
            || s.Equals("nan", StringComparison.OrdinalIgnoreCase)
            || s.Equals("nat", StringComparison.OrdinalIgnoreCase)
            || s.Length == 0)
            return null;
        var head = s.Length >= 10 ? s[..10] : s;
        return DateTime.TryParseExact(head, Formats,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt)
            ? dt
            : null;
    }
}
