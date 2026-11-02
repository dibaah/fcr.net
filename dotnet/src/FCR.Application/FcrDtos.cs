namespace FCR.Application;

/// <summary>Write model — parity with the FCRRecord pydantic model.</summary>
public sealed class FcrRecordInput
{
    public long? Id { get; set; }
    public string? RowNum { get; set; }
    public string? FcrSubject { get; set; }
    public string? Category { get; set; }
    public string? FcrCode { get; set; }
    public string? Date { get; set; }
    public string? ReviewDate { get; set; }
    public string? Originator { get; set; }
    public string? ActionParty { get; set; }
    public string? Level { get; set; }
    public string? SessionCode { get; set; }
    public string? PedecStatus { get; set; }
    public string? FcrStatus { get; set; }
    public string? ExecutionStatus { get; set; }
    public string? ClosedDate { get; set; }
    public string? Tr { get; set; }
    public string? NextActionBy { get; set; }
    public string? ArchLoc { get; set; }
    public string? LastUpdate { get; set; }
    public string? Remarks { get; set; }
}

/// <summary>Query model — parity with GET /api/fcr parameters.</summary>
public sealed record FcrQuery(
    string? Search = null,
    string? PedecStatus = null,
    string? FcrStatus = null,
    string? ExecStatus = null,
    string? Category = null,
    string? StatusGroup = null,
    string SortBy = "date",
    string SortOrder = "desc");

public sealed record BulkSaveRequest(List<FcrRecordInput> Records);
public sealed record BulkDeleteRequest(List<long> Ids);
public sealed record BulkSaveResult(int Saved, int Skipped, List<string> Errors);
