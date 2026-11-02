using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FCR.Application;
using FCR.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FCR.Infrastructure.Excel;

/// <summary>
/// Excel export — parity with app/services/export.py: styled "FCR Database"
/// sheet (header fill, per-row status colours, widths, freeze, autofilter)
/// plus a "Charts & Summary" sheet; native charts are injected post-save
/// because ClosedXML cannot create chart objects.
/// </summary>
public sealed class ExcelExportService(
    FCR.Infrastructure.Persistence.FcrDbContext db,
    IStatsService stats,
    IOptions<FcrOptions> options) : IExcelExportService
{
    private static readonly string[] Headers = FcrConstants.ExcelHeaders;
    private static readonly string[] DbMap = FcrConstants.ExcelDbFields;
    private static readonly double[] ColWidths =
        [6, 45, 12, 18, 12, 12, 15, 18, 10, 10, 20, 18, 18, 12, 6, 12, 10, 12, 40];
    private static readonly HashSet<string> DateFields = new(FcrConstants.DateFields);

    private static readonly Dictionary<string, (string Bg, string Fg)> Colors = new()
    {
        [StatusColor.Red] = ("FFEBEE", "C62828"),
        [StatusColor.Green] = ("E8F5E9", "2E7D32"),
        [StatusColor.Yellow] = ("FFFDE7", "F57F17"),
        [StatusColor.None] = ("FFFFFF", "212121"),
    };

    private readonly FcrOptions _o = options.Value;

    public byte[]? BuildExport(bool withCharts)
    {
        var records = db.Fcrs.AsNoTracking().OrderBy(f => f.Id).ToList();
        if (records.Count == 0) return null;

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("FCR Database");

        for (var ci = 0; ci < Headers.Length; ci++)
        {
            var cell = ws.Cell(1, ci + 1);
            cell.Value = Headers[ci];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.FromHtml("#FFFFFF");
            cell.Style.Font.FontSize = 10;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3C72");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#000000");
            ws.Column(ci + 1).Width = ColWidths[ci];
        }
        ws.Row(1).Height = 28;
        ws.SheetView.FreezeRows(1);

        for (var ri = 0; ri < records.Count; ri++)
        {
            var rec = records[ri];
            var (bg, fg) = Colors[StatusColor.Get(rec.PedecStatus, rec.FcrStatus, rec.ExecutionStatus)];
            for (var ci = 0; ci < DbMap.Length; ci++)
            {
                var field = DbMap[ci];
                var value = GetField(rec, field);
                if (DateFields.Contains(field)) value = ValueCleaning.CleanDate(value);
                var cell = ws.Cell(ri + 2, ci + 1);
                cell.Value = value;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#" + bg);
                cell.Style.Font.FontColor = XLColor.FromHtml("#" + fg);
                cell.Style.Font.FontSize = 9;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E0E0E0");
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                cell.Style.Alignment.WrapText = true;
            }
        }
        ws.Range(1, 1, 1, Headers.Length).SetAutoFilter();

        if (withCharts)
        {
            var s = stats.GetStats();
            AddChartsSheet(wb, s);
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return InjectCharts(ms.ToArray(), s);
        }

        using var outMs = new MemoryStream();
        wb.SaveAs(outMs);
        return outMs.ToArray();
    }

    public Dictionary<string, object?> ExportToFile(string destPath)
    {
        var bytes = BuildExport(withCharts: true);
        if (bytes is null) return new Dictionary<string, object?> { ["ok"] = false, ["error"] = "No records" };
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.WriteAllBytes(destPath, bytes);
            return new Dictionary<string, object?>
            {
                ["ok"] = true, ["path"] = destPath,
                ["size_kb"] = new FileInfo(destPath).Length / 1024,
            };
        }
        catch (Exception exc)
        {
            return new Dictionary<string, object?> { ["ok"] = false, ["error"] = exc.Message };
        }
    }

    private static void AddChartsSheet(XLWorkbook wb, Dictionary<string, object?> s)
    {
        var ws = wb.Worksheets.Add("Charts & Summary");
        ws.Cell(1, 1).Value = "FCR Summary Report";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#1E3C72");
        ws.Cell(2, 1).Value = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}";
        ws.Cell(3, 1).Value = "Total FCRs:";
        ws.Cell(3, 2).Value = (long)(s["total"] ?? 0L);
        ws.Cell(3, 2).Style.Font.Bold = true;

        (string Label, object? Val)[] summary =
        [
            ("Open / Active", s["open"]), ("Finished / Closed", s["finished"]),
            ("In Progress", s["pending"]), ("Rejected", s["rejected"]),
        ];
        for (var i = 0; i < summary.Length; i++)
        {
            ws.Cell(4 + i, 1).Value = summary[i].Label;
            ws.Cell(4 + i, 2).Value = Convert.ToInt64(summary[i].Val ?? 0);
        }

        WriteTable(ws, 11, 1, "FCRs by Year", "Year",
            (s["trend"] as List<Dictionary<string, object?>>)?.Select(r => (r["yr"]?.ToString() ?? "", r["cnt"])));
        WriteTable(ws, 11, 6, "By Category", "Category",
            (s["by_category"] as List<Dictionary<string, object?>>)?.Select(r => (r["category"]?.ToString() ?? "", r["cnt"])));
        WriteTable(ws, 11, 11, "By Execution Status", "Status",
            (s["by_exec_status"] as List<Dictionary<string, object?>>)?.Select(r => (r["execution_status"]?.ToString() ?? "", r["cnt"])));
    }

    private static void WriteTable(IXLWorksheet ws, int row, int col, string title, string catHeader,
        IEnumerable<(string Cat, object? Count)>? rows)
    {
        if (rows is null) return;
        var list = rows.ToList();
        if (list.Count == 0) return;
        ws.Cell(row, col).Value = title;
        ws.Cell(row, col).Style.Font.Bold = true;
        ws.Cell(row, col).Style.Font.FontSize = 12;
        ws.Cell(row + 1, col).Value = catHeader;
        ws.Cell(row + 1, col + 1).Value = "Count";
        for (var i = 0; i < list.Count; i++)
        {
            ws.Cell(row + 2 + i, col).Value = list[i].Cat;
            ws.Cell(row + 2 + i, col + 1).Value = Convert.ToInt64(list[i].Count ?? 0);
        }
    }

    /// <summary>Adds bar/pie chart parts referencing the Charts &amp; Summary ranges.</summary>
    private static byte[] InjectCharts(byte[] xlsx, Dictionary<string, object?> s)
    {
        var trendCount = (s["trend"] as List<Dictionary<string, object?>>)?.Count ?? 0;
        var catCount = (s["by_category"] as List<Dictionary<string, object?>>)?.Count ?? 0;
        var execCount = (s["by_exec_status"] as List<Dictionary<string, object?>>)?.Count ?? 0;
        if (trendCount + catCount + execCount == 0) return xlsx;

        using var ms = new MemoryStream();
        ms.Write(xlsx, 0, xlsx.Length);
        ms.Position = 0;
        using (var doc = SpreadsheetDocument.Open(ms, true))
        {
            var wbPart = doc.WorkbookPart!;
            var wsPart = wbPart.WorksheetParts
                .First(p => GetSheetName(wbPart, p) == "Charts & Summary");
            var drawingsPart = wsPart.AddNewPart<DrawingsPart>();
            var relId = wsPart.GetIdOfPart(drawingsPart);
            wsPart.Worksheet.Append(new Drawing { Id = relId });
            wsPart.Worksheet.Save();

            var wsDrawing = drawingsPart.WorksheetDrawing ??= new DocumentFormat.OpenXml.Drawing.Spreadsheet.WorksheetDrawing();
            var frameIndex = 1;
            if (trendCount > 0)
                AddChart(drawingsPart, wsDrawing, frameIndex++, 3, 0, "FCRs by Year",
                    BarChartXml("'Charts &amp; Summary'!$A$13:$A$" + (12 + trendCount),
                                "'Charts &amp; Summary'!$B$13:$B$" + (12 + trendCount),
                                "'Charts &amp; Summary'!$B$12"));
            if (catCount > 0)
                AddChart(drawingsPart, wsDrawing, frameIndex++, 3, 22, "By Category",
                    PieChartXml("'Charts &amp; Summary'!$F$13:$F$" + (12 + catCount),
                                "'Charts &amp; Summary'!$G$13:$G$" + (12 + catCount),
                                "'Charts &amp; Summary'!$G$12"));
            if (execCount > 0)
                AddChart(drawingsPart, wsDrawing, frameIndex++, 3, 44, "Execution Status",
                    BarChartXml("'Charts &amp; Summary'!$K$13:$K$" + (12 + execCount),
                                "'Charts &amp; Summary'!$L$13:$L$" + (12 + execCount),
                                "'Charts &amp; Summary'!$L$12"));
            drawingsPart.WorksheetDrawing.Save();
        }
        return ms.ToArray();
    }

    private static string GetSheetName(WorkbookPart wbPart, WorksheetPart wsPart)
    {
        var relId = wbPart.GetIdOfPart(wsPart);
        var sheet = wbPart.Workbook.Descendants<Sheet>().First(s => s.Id == relId);
        return sheet.Name!;
    }

    private static void AddChart(DrawingsPart drawingsPart, DocumentFormat.OpenXml.Drawing.Spreadsheet.WorksheetDrawing wsDrawing,
        int frameId, int col, int row, string title, string chartXml)
    {
        var chartPart = drawingsPart.AddNewPart<ChartPart>();
        var chartRelId = drawingsPart.GetIdOfPart(chartPart);
        var chartSpace = new DocumentFormat.OpenXml.Drawing.Charts.ChartSpace();
        chartSpace.InnerXml = chartXml;
        chartPart.ChartSpace = chartSpace;
        chartPart.ChartSpace.Save();

        // twoCellAnchor: place at col,row with ~15cm x ~10cm extent
        var anchor = new DocumentFormat.OpenXml.Drawing.Spreadsheet.TwoCellAnchor(
            new DocumentFormat.OpenXml.Drawing.Spreadsheet.FromMarker(
                new DocumentFormat.OpenXml.Drawing.Spreadsheet.ColumnId(col.ToString()),
                new DocumentFormat.OpenXml.Drawing.Spreadsheet.ColumnOffset("0"),
                new DocumentFormat.OpenXml.Drawing.Spreadsheet.RowId(row.ToString()),
                new DocumentFormat.OpenXml.Drawing.Spreadsheet.RowOffset("0")),
            new DocumentFormat.OpenXml.Drawing.Spreadsheet.ToMarker(
                new DocumentFormat.OpenXml.Drawing.Spreadsheet.ColumnId((col + 8).ToString()),
                new DocumentFormat.OpenXml.Drawing.Spreadsheet.ColumnOffset("0"),
                new DocumentFormat.OpenXml.Drawing.Spreadsheet.RowId((row + 20).ToString()),
                new DocumentFormat.OpenXml.Drawing.Spreadsheet.RowOffset("0")),
            new DocumentFormat.OpenXml.Drawing.Spreadsheet.GraphicFrame(
                new DocumentFormat.OpenXml.Drawing.Spreadsheet.NonVisualGraphicFrameProperties(
                    new DocumentFormat.OpenXml.Drawing.Spreadsheet.NonVisualDrawingProperties
                        { Id = (uint)frameId + 1, Name = "Chart " + frameId },
                    new DocumentFormat.OpenXml.Drawing.Spreadsheet.NonVisualGraphicFrameDrawingProperties()),
                new DocumentFormat.OpenXml.Drawing.Spreadsheet.Transform(),
                new DocumentFormat.OpenXml.Drawing.Graphic(
                    new DocumentFormat.OpenXml.Drawing.GraphicData(
                        new DocumentFormat.OpenXml.Drawing.Charts.ChartReference { Id = chartRelId })
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart" })),
            new DocumentFormat.OpenXml.Drawing.Spreadsheet.ClientData());
        wsDrawing.Append(anchor);
    }

    private static string BarChartXml(string cats, string vals, string nameRef) => $$"""
        <c:chart xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
          <c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr/></a:pPr></a:p></c:rich></c:tx><c:layout/></c:title>
          <c:plotArea><c:layout/>
            <c:barChart><c:barDir val="col"/><c:grouping val="clustered"/>
              <c:ser><c:idx val="0"/><c:order val="0"/>
                <c:tx><c:strRef><c:f>{{nameRef}}</c:f></c:strRef></c:tx>
                <c:cat><c:strRef><c:f>{{cats}}</c:f></c:strRef></c:cat>
                <c:val><c:numRef><c:f>{{vals}}</c:f></c:numRef></c:val>
              </c:ser>
              <c:axId val="111111111"/><c:axId val="222222222"/>
            </c:barChart>
            <c:catAx><c:axId val="111111111"/><c:scaling><c:orientation val="minMax"/></c:scaling>
              <c:axPos val="b"/><c:tickLblPos val="nextTo"/><c:crossAx val="222222222"/></c:catAx>
            <c:valAx><c:axId val="222222222"/><c:scaling><c:orientation val="minMax"/></c:scaling>
              <c:axPos val="l"/><c:tickLblPos val="nextTo"/><c:crossAx val="111111111"/></c:valAx>
          </c:plotArea>
        </c:chart>
        """;

    private static string PieChartXml(string cats, string vals, string nameRef) => $$"""
        <c:chart xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
          <c:plotArea><c:layout/>
            <c:pieChart>
              <c:ser><c:idx val="0"/><c:order val="0"/>
                <c:tx><c:strRef><c:f>{{nameRef}}</c:f></c:strRef></c:tx>
                <c:cat><c:strRef><c:f>{{cats}}</c:f></c:strRef></c:cat>
                <c:val><c:numRef><c:f>{{vals}}</c:f></c:numRef></c:val>
              </c:ser>
            </c:pieChart>
          </c:plotArea>
        </c:chart>
        """;

    private static string GetField(FCR.Domain.Entities.Fcr f, string field) => field switch
    {
        "row_num" => f.RowNum ?? "", "fcr_subject" => f.FcrSubject ?? "",
        "category" => f.Category ?? "", "fcr_code" => f.FcrCode ?? "",
        "date" => f.Date ?? "", "review_date" => f.ReviewDate ?? "",
        "originator" => f.Originator ?? "", "action_party" => f.ActionParty ?? "",
        "level" => f.Level ?? "", "session_code" => f.SessionCode ?? "",
        "pedec_status" => f.PedecStatus ?? "", "fcr_status" => f.FcrStatus ?? "",
        "execution_status" => f.ExecutionStatus ?? "", "closed_date" => f.ClosedDate ?? "",
        "tr" => f.Tr ?? "", "next_action_by" => f.NextActionBy ?? "",
        "arch_loc" => f.ArchLoc ?? "", "last_update" => f.LastUpdate ?? "",
        "remarks" => f.Remarks ?? "", _ => "",
    };
}
