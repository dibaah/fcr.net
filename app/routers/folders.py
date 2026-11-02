"""
Folder operations endpoints.
"""
from __future__ import annotations

import logging
import os
import subprocess
from fastapi import APIRouter, HTTPException

from app.core.db import db_connection, rdict
from app.services.fcr_code import find_existing_folder, generate_folder_path
from app.services.settings import load_settings

log = logging.getLogger("fcr.router.folders")

router = APIRouter(prefix="/api", tags=["Folders"])


@router.get("/fcr/{fcr_id}/open-folder")
def open_folder(fcr_id: int) -> dict:
    with db_connection() as conn:
        row = conn.execute("SELECT * FROM fcr WHERE id=?", (fcr_id,)).fetchone()
        if not row:
            raise HTTPException(404, "FCR not found")
        rec = rdict(row)
        fcr_code = (rec.get("fcr_code") or "").strip()

        if not fcr_code:
            return {"ok": False, "error": "FCR Code is empty"}

        settings = load_settings()
        parent_path = settings.get("attachment_root", r"X:\1-DC")

        folder_path = find_existing_folder(parent_path, fcr_code)

        if not folder_path:
            folder_path = generate_folder_path(parent_path, fcr_code)
            if not folder_path:
                return {"ok": False, "error": f"Cannot derive a folder path from FCR Code '{fcr_code}'"}
            return {"ok": False, "error": f"Folder not found:\n{folder_path}"}

        if not os.path.isdir(folder_path):
            return {"ok": False, "error": f"Folder not found:\n{folder_path}"}

        try:
            os.startfile(folder_path)  # type: ignore[attr-defined]
        except Exception:
            subprocess.Popen(["explorer", folder_path], shell=True)

        return {"ok": True, "path": folder_path}


@router.post("/fcr/{fcr_id}/create-folder")
def create_folder(fcr_id: int) -> dict:
    with db_connection() as conn:
        row = conn.execute("SELECT * FROM fcr WHERE id=?", (fcr_id,)).fetchone()
        if not row:
            raise HTTPException(404, "FCR not found")
        rec = rdict(row)
        fcr_code = (rec.get("fcr_code") or "").strip()
        if not fcr_code:
            return {"ok": False, "error": "FCR Code is empty"}

        settings = load_settings()
        parent_path = settings.get("attachment_root", r"X:\1-DC")
        folder_path = find_existing_folder(parent_path, fcr_code) or generate_folder_path(parent_path, fcr_code)
        if not folder_path:
            return {"ok": False, "error": "Cannot generate folder path from FCR Code"}

        try:
            from pathlib import Path
            Path(folder_path).mkdir(parents=True, exist_ok=True)
        except Exception as exc:
            return {"ok": False, "error": f"Cannot create folder:\n{folder_path}\n{exc}"}

        return {"ok": True, "path": folder_path}