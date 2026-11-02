namespace FCR.Domain;

/// <summary>Constants shared across layers — parity with app/core/config.py.</summary>
public static class FcrConstants
{
    public const string AppVersion = "2.0.0";
    public const string AppName = "FCR Dashboard";
    public const int SchemaVersion = 2;
    public const int MaxBackups = 20;

    /// <summary>FCR code scheme: FCR-&lt;AREA&gt;-&lt;DEPT&gt;-&lt;NNN&gt;</summary>
    public const string FcrCodePattern = @"^FCR-([A-Z]{2})-([A-Z]{2})-(\d{1,6})$";

    public static readonly IReadOnlyDictionary<string, string> AreaFolderMap =
        new Dictionary<string, string>
        {
            ["CT"] = "CTEP",
            ["EP"] = "EPL",
            ["FM"] = "FILMS",
            ["WL"] = "WELLS",
            ["MS"] = "Miscellaneous",
        };

    public static readonly IReadOnlyDictionary<string, string> DefaultDeptCodes =
        new Dictionary<string, string>
        {
            ["PI"] = "Piping", ["ME"] = "Mechanical",
            ["IN"] = "Instrument", ["EL"] = "Electrical",
            ["HV"] = "HVAC", ["CR"] = "Corrosion",
            ["PR"] = "Process", ["CV"] = "Civil",
        };

    /// <summary>The 19 writable fcr columns, in schema order (excludes id/created_at/updated_at).</summary>
    public static readonly string[] FcrFields =
    [
        "row_num", "fcr_subject", "category", "fcr_code", "date", "review_date",
        "originator", "action_party", "level", "session_code", "pedec_status",
        "fcr_status", "execution_status", "closed_date", "tr", "next_action_by",
        "arch_loc", "last_update", "remarks",
    ];

    /// <summary>Columns normalised to YYYY-MM-DD on write (parity with clean_date).</summary>
    public static readonly string[] DateFields = ["date", "review_date", "closed_date", "last_update"];

    /// <summary>EXCEL_HEADERS / EXCEL_DB_MAP parity — export column headers and their fields.</summary>
    public static readonly string[] ExcelHeaders =
    [
        "Row", "FCR Subject", "Category", "FCR Code", "Date", "Review Date",
        "Originator", "Action Party", "Level", "Session Code", "PEDEC Status",
        "FCR Status", "Execution Status", "Closed Date", "TR", "Next Action By",
        "Arch Loc", "Last Update", "Remarks",
    ];

    public static readonly string[] ExcelDbFields =
    [
        "row_num", "fcr_subject", "category", "fcr_code", "date", "review_date",
        "originator", "action_party", "level", "session_code", "pedec_status",
        "fcr_status", "execution_status", "closed_date", "tr", "next_action_by",
        "arch_loc", "last_update", "remarks",
    ];
}
