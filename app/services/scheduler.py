"""
Automatic export and backup scheduler.
"""
from __future__ import annotations

import logging
import threading
from datetime import datetime, timedelta
from typing import Optional

from app.services.settings import load_settings, save_settings, resolve_user_path
from app.services.export import export_excel_to_file
from app.services.backup import backup_dir, copy_db, prune_backups
from app.core.config import MAX_BACKUPS

log = logging.getLogger("fcr.scheduler")

_stop_event = threading.Event()
_background_started = False


def parse_date(value: str) -> Optional[datetime]:
    s = (value or "").strip()
    if not s:
        return None
    for fmt in ("%Y-%m-%d", "%Y/%m/%d", "%d/%m/%Y", "%m/%d/%Y"):
        try:
            return datetime.strptime(s[:10], fmt)
        except ValueError:
            continue
    return None


def due(last_value: str, interval_days: int) -> bool:
    last = parse_date(last_value or "")
    return not last or datetime.now() - last >= timedelta(days=interval_days)


def maybe_auto_export() -> None:
    settings = load_settings()
    if not settings.get("auto_export_enabled"):
        return
    interval_days = max(1, int(settings.get("auto_export_interval_days", 2) or 2))
    if not due(settings.get("last_auto_export", ""), interval_days):
        return
    export_dir = resolve_user_path(settings.get("auto_export_location", "exports"), "exports")
    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    result = export_excel_to_file(export_dir / f"FCR_AutoExport_{ts}.xlsx")
    if result.get("ok"):
        settings["last_auto_export"] = datetime.now().isoformat(timespec="seconds")
        save_settings(settings)
        log.info("Auto Excel export: %s", result["path"])


def maybe_auto_backup() -> None:
    settings = load_settings()
    if not settings.get("auto_backup_enabled", True):
        return
    interval_days = max(1, int(settings.get("auto_backup_interval_days", 1) or 1))
    if not due(settings.get("last_auto_backup", ""), interval_days):
        return
    bd = backup_dir()
    bd.mkdir(parents=True, exist_ok=True)
    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = bd / f"fcr_database_backup_{ts}.db"
    copy_db(dst)
    prune_backups()
    settings["last_auto_backup"] = datetime.now().isoformat(timespec="seconds")
    save_settings(settings)
    log.info("Auto backup: %s", dst)


def background_loop() -> None:
    """One scheduler for automatic Excel export and automatic DB backup."""
    while not _stop_event.wait(60):
        try:
            maybe_auto_export()
        except Exception as exc:
            log.warning("Auto export check failed: %s", exc)
        try:
            maybe_auto_backup()
        except Exception as exc:
            log.warning("Auto backup check failed: %s", exc)


def start_scheduler() -> None:
    global _background_started
    if not _background_started:
        threading.Thread(target=background_loop, daemon=True, name="fcr-scheduler").start()
        _background_started = True


def stop_scheduler() -> None:
    _stop_event.set()