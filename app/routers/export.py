"""
Excel export endpoints.
"""
from __future__ import annotations

from datetime import datetime
from fastapi import APIRouter, HTTPException
from fastapi.responses import StreamingResponse

from app.services.export import (
    fetch_records, build_workbook, add_charts_sheet, xlsx_response, export_excel_to_file
)
from app.routers.stats import get_stats

router = APIRouter(prefix="/api", tags=["Export"])


@router.get("/export")
def export_excel() -> StreamingResponse:
    records = fetch_records()
    if not records:
        raise HTTPException(404, "No records to export")
    wb = build_workbook(records)
    return xlsx_response(wb, f"FCR_Export_{datetime.now().strftime('%Y-%m-%d')}.xlsx")


@router.get("/export-with-charts")
def export_excel_with_charts() -> StreamingResponse:
    records = fetch_records()
    if not records:
        raise HTTPException(404, "No records to export")
    stats = get_stats()
    wb = build_workbook(records)
    add_charts_sheet(wb, stats)
    return xlsx_response(wb, f"FCR_Report_{datetime.now().strftime('%Y-%m-%d')}.xlsx")


@router.post("/auto-export/run")
def run_auto_export_now() -> dict:
    from app.services.settings import load_settings, resolve_user_path
    settings = load_settings()
    export_dir = resolve_user_path(settings.get("auto_export_location", "exports"), "exports")
    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    result = export_excel_to_file(export_dir / f"FCR_AutoExport_{ts}.xlsx")
    if result.get("ok"):
        settings["last_auto_export"] = datetime.now().isoformat(timespec="seconds")
        from app.services.settings import save_settings
        save_settings(settings)
    return result