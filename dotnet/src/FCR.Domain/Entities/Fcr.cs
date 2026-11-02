namespace FCR.Domain.Entities;

/// <summary>
/// FCR technical record — mirrors the existing `fcr` table (schema v2).
/// All non-key columns are TEXT in SQLite and stay strings for parity:
/// the Python app stores free-form values and cleans them on write.
/// </summary>
public class Fcr
{
    public long Id { get; set; }
    public string RowNum { get; set; } = "";
    public string FcrSubject { get; set; } = "";
    public string Category { get; set; } = "";
    public string FcrCode { get; set; } = "";
    public string Date { get; set; } = "";
    public string ReviewDate { get; set; } = "";
    public string Originator { get; set; } = "";
    public string ActionParty { get; set; } = "";
    public string Level { get; set; } = "";
    public string SessionCode { get; set; } = "";
    public string PedecStatus { get; set; } = "";
    public string FcrStatus { get; set; } = "";
    public string ExecutionStatus { get; set; } = "";
    public string ClosedDate { get; set; } = "";
    public string Tr { get; set; } = "";
    public string NextActionBy { get; set; } = "";
    public string ArchLoc { get; set; } = "";
    public string LastUpdate { get; set; } = "";
    public string Remarks { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
}
