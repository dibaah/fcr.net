"""
Database backup with SQLite backup API (WAL-safe).
"""
from __future__ import annotations

import logging
import sqlite3
from datetime import datetime
from pathlib import Path

from app.core.config import MAX_BACKUPS, DB_PATH
from app.services.settings import load_settings, resolve_user_path

log = logging.getLogger("fcr.backup")


def backup_dir() -> Path:
    s = load_settings()
    return resolve_user_path(s.get("backup_location", "backups"), "backups")


def copy_db(dst: Path) -> None:
    """Consistent SQLite copy — safe while the app is running (WAL mode)."""
    if DB_PATH.exists():
        src = sqlite3.connect(str(DB_PATH))
        try:
            dest = sqlite3.connect(str(dst))
            try:
                src.backup(dest)
            finally:
                dest.close()
        finally:
            src.close()
    else:
        raise FileNotFoundError(f"Database not found: {DB_PATH}")


def prune_backups(limit: int = MAX_BACKUPS) -> int:
    bd = backup_dir()
    if not bd.exists():
        return 0
    files = sorted(bd.glob("fcr_database_backup_*.db"), key=lambda f: f.stat().st_mtime, reverse=True)
    removed = 0
    for old in files[limit:]:
        try:
            old.unlink()
            removed += 1
        except OSError as exc:
            log.warning("Could not remove old backup %s: %s", old, exc)
    return removed


def create_backup() -> dict:
    bd = backup_dir()
    bd.mkdir(parents=True, exist_ok=True)
    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = bd / f"fcr_database_backup_{ts}.db"
    try:
        copy_db(dst)
        pruned = prune_backups()
        log.info("Backup: %s (pruned %d)", dst, pruned)
        return {
            "ok": True, "path": str(dst),
            "size_kb": dst.stat().st_size // 1024, "timestamp": ts,
            "pruned": pruned,
        }
    except Exception as exc:
        raise RuntimeError(f"Backup failed: {exc}")


def list_backups() -> list[dict]:
    bd = backup_dir()
    if not bd.exists():
        return []
    return [
        {
            "filename": f.name, "path": str(f),
            "size_kb": f.stat().st_size // 1024,
            "created": datetime.fromtimestamp(f.stat().st_mtime).strftime("%Y-%m-%d %H:%M"),
        }
        for f in sorted(bd.glob("fcr_database_backup_*.db"), key=lambda f: f.stat().st_mtime, reverse=True)[:MAX_BACKUPS]
    ]