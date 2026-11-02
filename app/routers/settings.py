"""
Settings endpoints.
"""
from __future__ import annotations

from fastapi import APIRouter, HTTPException

from app.models import SettingsModel
from app.services.settings import load_settings, save_settings, settings_payload, reset_settings

router = APIRouter(prefix="/api", tags=["Settings"])


@router.get("/settings")
def get_settings() -> dict:
    return settings_payload(load_settings())


@router.put("/settings")
def update_settings(body: SettingsModel) -> dict:
    cur = load_settings()
    data = body.model_dump(exclude_none=True)
    for key in ("auto_export_interval_days", "auto_backup_interval_days", "pedec_alert_days"):
        if key in data:
            data[key] = max(1, int(data[key] or 1))
    if "language" in data and data["language"] not in ("en", "zh"):
        data["language"] = "en"
    cur.update(data)
    save_settings(cur)
    return settings_payload(load_settings())


@router.post("/settings/reset")
def reset_settings_endpoint() -> dict:
    try:
        reset_settings()
        return {"ok": True, "message": "Settings reset to defaults"}
    except Exception as exc:
        raise HTTPException(500, f"Reset failed: {exc}")