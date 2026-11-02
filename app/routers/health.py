"""
Health and about endpoints.
"""
from __future__ import annotations

import platform
from datetime import datetime
from fastapi import APIRouter

from app.core.config import APP_VERSION, APP_NAME, DB_PATH, BASE_DIR, STATIC_DIR, SCHEMA_VERSION, _FROZEN
from app.core.db import db_connection

router = APIRouter(prefix="/api", tags=["Health"])


@router.get("/health")
def health() -> dict:
    return {
        "status": "ok",
        "version": APP_VERSION,
        "db": str(DB_PATH),
        "data_dir": str(BASE_DIR),
        "python": platform.python_version(),
        "time": datetime.now().isoformat(timespec="seconds"),
    }


@router.get("/about")
def about() -> dict:
    with db_connection() as conn:
        total = conn.execute("SELECT COUNT(*) FROM fcr").fetchone()[0]
    return {
        "app_name": APP_NAME,
        "version": APP_VERSION,
        "author": "Farzad Meshkipour",
        "company": "SINOPEC International — YADAVARAN",
        "python": platform.python_version(),
        "platform": platform.platform(),
        "frozen": _FROZEN,
        "data_dir": str(BASE_DIR),
        "db_path": str(DB_PATH),
        "static_dir": str(STATIC_DIR),
        "schema_version": SCHEMA_VERSION,
        "records": total,
    }