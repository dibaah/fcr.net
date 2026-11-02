using System.Text.RegularExpressions;

namespace FCR.Domain;

/// <summary>Value normalisation — parity with clean()/clean_date() in the Python app.</summary>
public static partial class ValueCleaning
{
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}")]
    private static partial Regex IsoDatePrefix();

    /// <summary>null/"none"/"nan"/"nat"/whitespace → "", else trimmed string.</summary>
    public static string Clean(object? value)
    {
        var s = (value?.ToString() ?? "").Trim();
        return s.Equals("none", StringComparison.OrdinalIgnoreCase)
            || s.Equals("nan", StringComparison.OrdinalIgnoreCase)
            || s.Equals("nat", StringComparison.OrdinalIgnoreCase)
                ? ""
                : s;
    }

    /// <summary>Clean + truncate to YYYY-MM-DD when it starts with an ISO date.</summary>
    public static string CleanDate(object? value)
    {
        var s = Clean(value);
        return s.Length >= 10 && IsoDatePrefix().IsMatch(s) ? s[..10] : s;
    }
}
