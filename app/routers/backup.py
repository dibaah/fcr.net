"""
Backup endpoints.
"""
from __future__ import annotations

from fastapi import APIRouter, HTTPException

from app.services.backup import create_backup, list_backups

router = APIRouter(prefix="/api", tags=["Backup"])


@router.post("/backup")
def backup_db() -> dict:
    try:
        return create_backup()
    except Exception as exc:
        raise HTTPException(500, f"Backup failed: {exc}")


@router.get("/backups")
def list_backups_endpoint() -> list[dict]:
    return list_backups()