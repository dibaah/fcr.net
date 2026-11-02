"""
Excel export with charts.
"""
from __future__ import annotations

import io
import logging
from datetime import datetime
from pathlib import Path
from typing import Any, Optional

import openpyxl
from fastapi.responses import StreamingResponse
from openpyxl.chart import BarChart, PieChart, Reference
from openpyxl.styles import Alignment, Border, Font, PatternFill, Side
from openpyxl.utils import get_column_letter

from app.core.config import (
    EXCEL_HEADERS, EXCEL_DB_MAP, COL_WIDTHS, DATE_FIELDS,
    EXCEL_COLORS, _XLSX_MEDIA_TYPE,
)
from app.core.db import db_connection, rdict
from app.services.fcr_code import get_color
from app.services.settings import load_settings

log = logging.getLogger("fcr.export")


def fetch_records() -> list[dict]:
    with db_connection() as conn:
        return [rdict(r) for r in conn.execute("SELECT * FROM fcr ORDER BY id ASC").fetchall()]


def clean(v: Any) -> str:
    if v is None:
        return ""
    s = str(v).strip()
    return "" if s.lower() in ("none", "nan", "nat") else s


def clean_date(v: Any) -> str:
    s = clean(v)
    return s[:10] if len(s) >= 10 and __import__("re").match(r"^\d{4}-\d{2}-\d{2}", s) else s


def build_workbook(records: list[dict], stats: Optional[dict] = None) -> openpyxl.Workbook:
    """Shared workbook builder so both exports stay in sync."""
    thin = lambda c: Side(style="thin", color=c)
    header_border = Border(thin("000000"), thin("000000"), thin("000000"), thin("000000"))
    data_border = Border(thin("E0E0E0"), thin("E0E0E0"), thin("E0E0E0"), thin("E0E0E0"))

    wb = openpyxl.Workbook()
    ws = wb.active
    ws.title = "FCR Database"

    for ci, h in enumerate(EXCEL_HEADERS, 1):
        cell = ws.cell(row=1, column=ci, value=h)
        cell.font = Font(bold=True, color="FFFFFF", size=10)
        cell.fill = PatternFill("solid", fgColor="1E3C72")
        cell.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)
        cell.border = header_border
        ws.column_dimensions[get_column_letter(ci)].width = COL_WIDTHS[ci - 1]

    ws.row_dimensions[1].height = 28
    ws.freeze_panes = "A2"

    for ri, rec in enumerate(records, 2):
        bg, fc = EXCEL_COLORS[get_color(rec)]
        fill = PatternFill("solid", fgColor=bg)
        font = Font(color=fc, size=9)
        for ci, h in enumerate(EXCEL_HEADERS, 1):
            field = EXCEL_DB_MAP[h]
            value = rec.get(field, "") or ""
            if field in DATE_FIELDS:
                value = clean_date(value)
            cell = ws.cell(row=ri, column=ci, value=value)
            cell.fill = fill
            cell.font = font
            cell.border = data_border
            cell.alignment = Alignment(vertical="top", wrap_text=True)

    ws.auto_filter.ref = f"A1:{get_column_letter(len(EXCEL_HEADERS))}1"
    return wb


def add_charts_sheet(wb: openpyxl.Workbook, stats: dict) -> None:
    ws = wb.create_sheet(title="Charts & Summary")

    ws.cell(row=1, column=1, value="FCR Summary Report").font = Font(bold=True, size=16, color="1E3C72")
    ws.cell(row=2, column=1, value=f"Generated: {datetime.now().strftime('%Y-%m-%d %H:%M')}")
    ws.cell(row=3, column=1, value="Total FCRs:")
    ws.cell(row=3, column=2, value=stats.get("total", 0)).font = Font(bold=True)

    summary_rows = [
        ("Open / Active", stats.get("open", 0)),
        ("Finished / Closed", stats.get("finished", 0)),
        ("In Progress", stats.get("pending", 0)),
        ("Rejected", stats.get("rejected", 0)),
    ]
    for offset, (label, value) in enumerate(summary_rows, 4):
        ws.cell(row=offset, column=1, value=label)
        ws.cell(row=offset, column=2, value=value)

    trend = stats.get("trend") or []
    if trend:
        ws.cell(row=11, column=1, value="FCRs by Year").font = Font(bold=True, size=12)
        ws.cell(row=12, column=1, value="Year")
        ws.cell(row=12, column=2, value="Count")
        for i, item in enumerate(trend, 13):
            ws.cell(row=i, column=1, value=item["yr"])
            ws.cell(row=i, column=2, value=item["cnt"])

        chart = BarChart()
        data = Reference(ws, min_col=2, min_row=12, max_row=12 + len(trend))
        cats = Reference(ws, min_col=1, min_row=13, max_row=12 + len(trend))
        chart.add_data(data, titles_from_data=True)
        chart.set_categories(cats)
        chart.title = "FCRs by Year"
        chart.style = 13
        chart.width = 15
        chart.height = 10
        ws.add_chart(chart, "D11")

    by_category = stats.get("by_category") or []
    if by_category:
        ws.cell(row=11, column=6, value="By Category").font = Font(bold=True, size=12)
        ws.cell(row=12, column=6, value="Category")
        ws.cell(row=12, column=7, value="Count")
        for i, item in enumerate(by_category, 13):
            ws.cell(row=i, column=6, value=item["category"])
            ws.cell(row=i, column=7, value=item["cnt"])

        chart = PieChart()
        data = Reference(ws, min_col=7, min_row=12, max_row=12 + len(by_category))
        cats = Reference(ws, min_col=6, min_row=13, max_row=12 + len(by_category))
        chart.add_data(data, titles_from_data=True)
        chart.set_categories(cats)
        chart.title = "By Category"
        chart.style = 10
        chart.width = 15
        chart.height = 10
        ws.add_chart(chart, "D33")

    by_exec = stats.get("by_exec_status") or []
    if by_exec:
        ws.cell(row=11, column=11, value="By Execution Status").font = Font(bold=True, size=12)
        ws.cell(row=12, column=11, value="Status")
        ws.cell(row=12, column=12, value="Count")
        for i, item in enumerate(by_exec, 13):
            ws.cell(row=i, column=11, value=item["execution_status"])
            ws.cell(row=i, column=12, value=item["cnt"])

        chart = BarChart()
        data = Reference(ws, min_col=12, min_row=12, max_row=12 + len(by_exec))
        cats = Reference(ws, min_col=11, min_row=13, max_row=12 + len(by_exec))
        chart.add_data(data, titles_from_data=True)
        chart.set_categories(cats)
        chart.title = "Execution Status"
        chart.style = 12
        chart.width = 15
        chart.height = 10
        ws.add_chart(chart, "D55")


def xlsx_response(wb: openpyxl.Workbook, fname: str) -> StreamingResponse:
    buf = io.BytesIO()
    wb.save(buf)
    buf.seek(0)
    return StreamingResponse(
        buf,
        media_type=_XLSX_MEDIA_TYPE,
        headers={"Content-Disposition": f'attachment; filename="{fname}"'},
    )


def export_excel_to_file(dst: Path) -> dict:
    records = fetch_records()
    if not records:
        return {"ok": False, "error": "No records"}
    try:
        from app.routers.stats import get_stats
        wb = build_workbook(records)
        add_charts_sheet(wb, get_stats())
        dst.parent.mkdir(parents=True, exist_ok=True)
        wb.save(dst)
        log.info("Excel export written: %s", dst)
        return {"ok": True, "path": str(dst), "size_kb": dst.stat().st_size // 1024}
    except Exception as exc:
        log.error("Excel export failed: %s", exc)
        return {"ok": False, "error": str(exc)}