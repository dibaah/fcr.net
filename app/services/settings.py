"""
Settings load/save with path resolution.
"""
from __future__ import annotations

import json
import logging
import os
from pathlib import Path
from typing import Any

from app.core.config import SETTINGS_PATH, DEFAULT_SETTINGS, BASE_DIR

log = logging.getLogger("fcr.settings")


def load_settings() -> dict[str, Any]:
    try:
        if SETTINGS_PATH.exists():
            with open(SETTINGS_PATH, encoding="utf-8") as f:
                data = json.load(f)
            merged = {**DEFAULT_SETTINGS, **data}
            if "dept_codes" in data and isinstance(data["dept_codes"], dict):
                merged["dept_codes"] = {**DEFAULT_SETTINGS["dept_codes"], **data["dept_codes"]}
            return merged
    except Exception as exc:
        log.warning("settings.json unreadable (%s) — using defaults", exc)
    return dict(DEFAULT_SETTINGS)


def save_settings(data: dict[str, Any]) -> None:
    # Derived / read-only keys never belong in settings.json.
    readonly = {
        "db_location", "app_version", "backup_resolved",
        "auto_export_resolved", "auto_backup_resolved", "schema_version",
    }
    clean = {k: v for k, v in data.items() if k not in readonly}
    tmp = SETTINGS_PATH.with_suffix(".json.tmp")
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(clean, f, indent=2, ensure_ascii=False)
    os.replace(tmp, SETTINGS_PATH)


def reset_settings() -> None:
    if SETTINGS_PATH.exists():
        SETTINGS_PATH.unlink()
        log.info("Removed old settings.json")
    save_settings(DEFAULT_SETTINGS)
    log.info("Settings reset to defaults")


def resolve_user_path(value: str, default_name: str) -> Path:
    raw = (value or "").strip() or default_name
    p = Path(raw)
    return p if p.is_absolute() else BASE_DIR / p


def settings_payload(s: dict[str, Any]) -> dict:
    out = dict(s)
    from app.core.config import APP_VERSION, DB_PATH, SCHEMA_VERSION
    out["app_version"] = APP_VERSION
    out["db_location"] = str(DB_PATH)
    out["backup_resolved"] = str(resolve_user_path(s.get("backup_location", "backups"), "backups"))
    out["auto_export_resolved"] = str(resolve_user_path(s.get("auto_export_location", "exports"), "exports"))
    out["schema_version"] = SCHEMA_VERSION
    return out