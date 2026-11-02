using FCR.Application;
using FCR.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FCR.Infrastructure.Stats;

/// <summary>
/// Stats/options/notifications — exact parity with routers/stats.py,
/// including its SQL group-bys and notification heuristics.
/// </summary>
public sealed class StatsService(
    FCR.Infrastructure.Persistence.FcrDbContext db,
    ISettingsStore settingsStore) : IStatsService
{
    private SqliteConnection Conn => (SqliteConnection)db.Database.GetDbConnection();

    public Dictionary<string, object?> GetStats()
    {
        var conn = Conn;
        conn.Open();
        try
        {
            var total = Count(conn, "1=1");
            var finished = Count(conn, StatusGroups.Finished);
            var pending = Count(conn, StatusGroups.Pending);
            var rejected = Count(conn, StatusGroups.Rejected);
            var open = Count(conn, StatusGroups.Open);

            return new Dictionary<string, object?>
            {
                ["total"] = total, ["finished"] = finished, ["pending"] = pending,
                ["rejected"] = rejected, ["open"] = open,
                ["trend"] = Rows(conn,
                    "SELECT SUBSTR(date,1,4) yr, COUNT(*) cnt FROM fcr " +
                    "WHERE date!='' AND SUBSTR(date,1,4) GLOB '[0-9][0-9][0-9][0-9]' " +
                    "GROUP BY yr ORDER BY yr"),
                ["by_category"] = Rows(conn,
                    "SELECT category, COUNT(*) cnt FROM fcr WHERE category!='' GROUP BY category"),
                ["by_exec_status"] = Rows(conn,
                    "SELECT execution_status, COUNT(*) cnt FROM fcr WHERE execution_status!='' " +
                    "GROUP BY execution_status ORDER BY cnt DESC"),
                ["by_action_party"] = Rows(conn,
                    "SELECT action_party, COUNT(*) cnt FROM fcr WHERE action_party!='' " +
                    "GROUP BY action_party ORDER BY cnt DESC LIMIT 10"),
                ["by_level"] = Rows(conn,
                    "SELECT level, COUNT(*) cnt FROM fcr WHERE level!='' GROUP BY level ORDER BY level"),
            };
        }
        finally { conn.Close(); }
    }

    public Dictionary<string, object?> GetOptions()
    {
        var conn = Conn;
        conn.Open();
        try
        {
            List<string> Distinct(string col) => Rows(conn,
                $"SELECT DISTINCT {col} v FROM fcr WHERE {col}!='' ORDER BY {col}")
                .Select(r => (string)r["v"]!).ToList();
            return new Dictionary<string, object?>
            {
                ["pedec_status"] = Distinct("pedec_status"),
                ["fcr_status"] = Distinct("fcr_status"),
                ["execution_status"] = Distinct("execution_status"),
                ["category"] = Distinct("category"),
                ["action_party"] = Distinct("action_party"),
                ["originator"] = Distinct("originator"),
            };
        }
        finally { conn.Close(); }
    }

    public Dictionary<string, object?> GetNotifications()
    {
        var alertDays = Math.Max(1, settingsStore.Load().PedecAlertDays);
        var cutoff = DateTime.Now - TimeSpan.FromDays(alertDays);

        var rows = db.Fcrs.AsNoTracking()
            .Where(f => f.Date != ""
                && !(new[] { "closed", "rejected", "cancelled", "suspended" }
                    .Contains((f.FcrStatus ?? "").ToLower())))
            .ToList();

        var overdue = new List<Dictionary<string, object?>>();
        foreach (var rec in rows)
        {
            var p = (rec.PedecStatus ?? "").ToLowerInvariant();
            var f = (rec.FcrStatus ?? "").ToLowerInvariant();
            if (p.Contains("approved") || p.Contains("signed") || p.Contains("closed")) continue;

            var issuedToPedec =
                p.Contains("pending")
                || f.Contains("issued for afe")
                || f.Contains("issued to pedec")
                || (rec.ActionParty ?? "").Contains("pedec", StringComparison.OrdinalIgnoreCase)
                || (rec.NextActionBy ?? "").Contains("pedec", StringComparison.OrdinalIgnoreCase)
                || (rec.Remarks ?? "").Contains("pedec", StringComparison.OrdinalIgnoreCase);
            var dt = FcrDate.Parse(rec.Date);
            if (!issuedToPedec || dt is null || dt > cutoff) continue;

            overdue.Add(new Dictionary<string, object?>
            {
                ["id"] = rec.Id,
                ["fcr_code"] = rec.FcrCode ?? "",
                ["fcr_subject"] = rec.FcrSubject ?? "",
                ["date"] = rec.Date ?? "",
                ["age_days"] = (DateTime.Now - dt.Value).Days,
                ["pedec_status"] = rec.PedecStatus ?? "",
                ["fcr_status"] = rec.FcrStatus ?? "",
                ["action_party"] = rec.ActionParty ?? "",
            });
        }

        overdue.Sort((a, b) => ((int)b["age_days"]!).CompareTo((int)a["age_days"]!));
        return new Dictionary<string, object?>
        {
            ["count"] = overdue.Count,
            ["alert_days"] = alertDays,
            ["items"] = overdue.Take(50).ToList(),
        };
    }

    private static long Count(SqliteConnection conn, string where)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM fcr WHERE {where}";
        return (long)cmd.ExecuteScalar()!;
    }

    private static List<Dictionary<string, object?>> Rows(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }
}

/// <summary>STATUS_GROUPS SQL fragments — same expressions as app/core/config.py.</summary>
public static class StatusGroups
{
    public const string RejectedBase =
        "(LOWER(fcr_status) LIKE '%rejected%' OR LOWER(fcr_status) LIKE '%suspended%' " +
        "OR LOWER(fcr_status) LIKE '%cancelled%')";
    public const string FinishedBase =
        "(LOWER(execution_status) LIKE '%finished%' OR LOWER(fcr_status)='closed')";
    public const string PendingBase =
        "(LOWER(execution_status) LIKE '%pending%' OR LOWER(execution_status) LIKE '%awaiting%' " +
        "OR LOWER(execution_status) LIKE '%on-going%' OR LOWER(execution_status) LIKE '%preparation%')";
    public const string Open = $"(NOT {FinishedBase} AND NOT {RejectedBase})";
    public const string Finished = $"({FinishedBase} AND NOT {RejectedBase})";
    public const string Pending = $"({PendingBase} AND NOT {RejectedBase} AND NOT {FinishedBase})";
    public const string Rejected = RejectedBase;
}
