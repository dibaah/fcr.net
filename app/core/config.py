"""
Configuration and paths — single source of truth for runtime settings.
"""
from __future__ import annotations

import os
import sys
from pathlib import Path
from typing import Any

# ─────────────────────────────────────────────────────────────────
# Version — single source of truth
# ─────────────────────────────────────────────────────────────────
APP_VERSION = "2.0.0"
APP_NAME = "FCR Dashboard"

# ─────────────────────────────────────────────────────────────────
# Paths
# ─────────────────────────────────────────────────────────────────
_SOURCE_DIR = Path(__file__).resolve().parent.parent.parent
_FROZEN = bool(getattr(sys, "frozen", False))


def _resolve_base_dir() -> Path:
    override = os.environ.get("FCR_DATA_DIR", "").strip()
    if override:
        path = Path(override).expanduser()
        path.mkdir(parents=True, exist_ok=True)
        return path.resolve()
    if _FROZEN:
        return Path(sys.executable).resolve().parent
    return _SOURCE_DIR


BASE_DIR = _resolve_base_dir()

if _FROZEN and hasattr(sys, "_MEIPASS"):
    STATIC_DIR = Path(sys._MEIPASS) / "static"
else:
    STATIC_DIR = _SOURCE_DIR / "static"

DB_PATH = BASE_DIR / "fcr_database.db"
SETTINGS_PATH = BASE_DIR / "settings.json"
LOG_DIR = BASE_DIR / "logs"
LOG_DIR.mkdir(parents=True, exist_ok=True)

# ─────────────────────────────────────────────────────────────────
# Default settings
# ─────────────────────────────────────────────────────────────────
DEFAULT_SETTINGS: dict[str, Any] = {
    "company_name":    "SINOPEC International — YADAVARAN",
    "app_version":     APP_VERSION,
    "attachment_root": r"X:\1-DC",
    "backup_location": "backups",
    "auto_backup_enabled": True,
    "auto_backup_interval_days": 1,
    "last_auto_backup": "",
    "auto_export_enabled": False,
    "auto_export_location": "exports",
    "auto_export_interval_days": 2,
    "last_auto_export": "",
    "pedec_alert_days": 14,
    "dark_mode":       False,
    "language":        "en",
    "dept_codes": {
        "PI": "Piping",      "ME": "Mechanical",
        "IN": "Instrument",  "EL": "Electrical",
        "HV": "HVAC",        "CR": "Corrosion",
        "PR": "Process",     "CV": "Civil",
    },
}

# FCR code scheme: FCR-<AREA>-<DEPT>-<NNN>
AREA_FOLDER_MAP = {
    "CT": "CTEP",
    "EP": "EPL",
    "FM": "FILMS",
    "WL": "WELLS",
    "MS": "Miscellaneous",
}

FCR_CODE_RE = __import__("re").compile(r"^FCR-([A-Z]{2})-([A-Z]{2})-(\d{1,6})$")

# Status groups — shared by /api/stats and /api/fcr?status_group=…
_REJECTED_BASE = (
    "(LOWER(fcr_status) LIKE '%rejected%' OR LOWER(fcr_status) LIKE '%suspended%' "
    "OR LOWER(fcr_status) LIKE '%cancelled%')"
)
_FINISHED_BASE = (
    "(LOWER(execution_status) LIKE '%finished%' OR LOWER(fcr_status)='closed')"
)
_PENDING_BASE = (
    "(LOWER(execution_status) LIKE '%pending%' OR LOWER(execution_status) LIKE '%awaiting%' "
    "OR LOWER(execution_status) LIKE '%on-going%' OR LOWER(execution_status) LIKE '%preparation%')"
)
STATUS_GROUPS: dict[str, str] = {
    "open":     f"(NOT {_FINISHED_BASE} AND NOT {_REJECTED_BASE})",
    "finished": f"({_FINISHED_BASE} AND NOT {_REJECTED_BASE})",
    "pending":  f"({_PENDING_BASE} AND NOT {_REJECTED_BASE} AND NOT {_FINISHED_BASE})",
    "rejected": _REJECTED_BASE,
}

FCR_FIELDS = [
    "row_num", "fcr_subject", "category", "fcr_code", "date", "review_date",
    "originator", "action_party", "level", "session_code", "pedec_status",
    "fcr_status", "execution_status", "closed_date", "tr", "next_action_by",
    "arch_loc", "last_update", "remarks",
]

SCHEMA_VERSION = 2

MAX_BACKUPS = 20

EXCEL_HEADERS = [
    "Row", "FCR Subject", "Category", "FCR Code", "Date", "Review Date",
    "Originator", "Action Party", "Level", "Session Code", "PEDEC Status",
    "FCR Status", "Execution Status", "Closed Date", "TR", "Next Action By",
    "Arch Loc", "Last Update", "Remarks",
]
EXCEL_DB_MAP = {
    "Row": "row_num", "FCR Subject": "fcr_subject", "Category": "category",
    "FCR Code": "fcr_code", "Date": "date", "Review Date": "review_date",
    "Originator": "originator", "Action Party": "action_party", "Level": "level",
    "Session Code": "session_code", "PEDEC Status": "pedec_status",
    "FCR Status": "fcr_status", "Execution Status": "execution_status",
    "Closed Date": "closed_date", "TR": "tr", "Next Action By": "next_action_by",
    "Arch Loc": "arch_loc", "Last Update": "last_update", "Remarks": "remarks",
}
COL_WIDTHS = [6, 45, 12, 18, 12, 12, 15, 18, 10, 10, 20, 18, 18, 12, 6, 12, 10, 12, 40]
DATE_FIELDS = ("date", "review_date", "closed_date", "last_update")

EXCEL_COLORS = {
    "red":    ("FFEBEE", "C62828"),
    "green":  ("E8F5E9", "2E7D32"),
    "yellow": ("FFFDE7", "F57F17"),
    "none":   ("FFFFFF", "212121"),
}
_XLSX_MEDIA_TYPE = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"