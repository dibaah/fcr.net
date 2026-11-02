using FCR.Domain.Entities;

namespace FCR.Application;

/// <summary>Serializes a record the way the Python API does — all snake_case columns + folder_path.</summary>
public static class FcrJson
{
    public static Dictionary<string, object?> ToDict(Fcr f, string folderPath = "") =>
        new()
        {
            ["id"] = f.Id,
            ["row_num"] = f.RowNum,
            ["fcr_subject"] = f.FcrSubject,
            ["category"] = f.Category,
            ["fcr_code"] = f.FcrCode,
            ["date"] = f.Date,
            ["review_date"] = f.ReviewDate,
            ["originator"] = f.Originator,
            ["action_party"] = f.ActionParty,
            ["level"] = f.Level,
            ["session_code"] = f.SessionCode,
            ["pedec_status"] = f.PedecStatus,
            ["fcr_status"] = f.FcrStatus,
            ["execution_status"] = f.ExecutionStatus,
            ["closed_date"] = f.ClosedDate,
            ["tr"] = f.Tr,
            ["next_action_by"] = f.NextActionBy,
            ["arch_loc"] = f.ArchLoc,
            ["last_update"] = f.LastUpdate,
            ["remarks"] = f.Remarks,
            ["created_at"] = f.CreatedAt,
            ["updated_at"] = f.UpdatedAt,
            ["folder_path"] = folderPath,
        };
}
