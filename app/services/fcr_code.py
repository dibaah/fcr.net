"""
FCR code parsing, validation, and folder path generation.
"""
from __future__ import annotations

import os
import re
from datetime import datetime
from pathlib import Path
from typing import Any, Optional

from fastapi import HTTPException

from app.core.config import (
    AREA_FOLDER_MAP, FCR_CODE_RE, DEFAULT_SETTINGS,
)


def parse_fcr_code(code: str) -> tuple[str, str]:
    """Parse 'FCR-CT-PI-046' → ('CTEP', 'PI')."""
    if not code:
        return "", ""
    parts = re.split(r"[-_]", code.strip())
    area_abbrev = parts[1].upper() if len(parts) > 1 else ""
    dept_abbrev = parts[2].upper() if len(parts) > 2 else ""
    area_folder = AREA_FOLDER_MAP.get(area_abbrev, "")
    return area_folder, dept_abbrev


def generate_folder_path(parent_path: str, fcr_code: str) -> str:
    """Generate folder path from FCR code."""
    area_folder, dept_folder = parse_fcr_code(fcr_code)
    if not area_folder or not dept_folder:
        return ""
    return os.path.join(parent_path, area_folder, dept_folder, fcr_code)


def find_existing_folder(parent_path: str, fcr_code: str) -> Optional[str]:
    """Find existing folder (with or without title suffix)."""
    area_folder, dept_folder = parse_fcr_code(fcr_code)
    if not area_folder or not dept_folder:
        return None

    base_path = Path(parent_path) / area_folder / dept_folder
    if not base_path.exists():
        return None

    try:
        for item in base_path.iterdir():
            if item.is_dir() and item.name.startswith(fcr_code):
                return str(item)
    except Exception:
        pass
    return None


def get_color(rec: dict) -> str:
    p = (rec.get("pedec_status") or "").lower()
    f = (rec.get("fcr_status") or "").lower()
    e = (rec.get("execution_status") or "").lower()
    if any(k in f or k in p for k in ("rejected", "suspended", "cancelled")):
        return "red"
    if "finished" in e or f == "closed" or p == "closed":
        return "green"
    if any(k in e for k in ("pending", "awaiting", "on-going", "preparation")):
        return "yellow"
    if any(k in f for k in ("issued", "under evaluation", "expert approved", "commented")):
        return "yellow"
    return "none"


def parse_date(value: str) -> Optional[datetime]:
    s = str(value or "").strip()
    if s.lower() in ("", "none", "nan", "nat"):
        return None
    for fmt in ("%Y-%m-%d", "%Y/%m/%d", "%d/%m/%Y", "%m/%d/%Y"):
        try:
            return datetime.strptime(s[:10], fmt)
        except ValueError:
            continue
    return None


def normalize_fcr_code(value: Any) -> str:
    """Trim/upper-case, and collapse internal whitespace."""
    return re.sub(r"\s+", "", str(value or "")).upper()


def validate_fcr_code(value: Any) -> str:
    """Return the normalized code, or '' when blank. Raises HTTP 400 when malformed."""
    code = normalize_fcr_code(value)
    if not code:
        return ""
    match = FCR_CODE_RE.match(code)
    if not match:
        raise HTTPException(
            400,
            f"Invalid FCR Code {code!r}. Expected format FCR-<AREA>-<DEPT>-<NNN>, "
            f"e.g. FCR-CT-PI-046.",
        )
    area, dept = match.group(1), match.group(2)
    if area not in AREA_FOLDER_MAP:
        raise HTTPException(
            400,
            f"Unknown area code {area!r} in FCR Code {code!r}. "
            f"Allowed: {', '.join(sorted(AREA_FOLDER_MAP))}.",
        )
    allowed_depts = DEFAULT_SETTINGS["dept_codes"]
    if dept not in allowed_depts:
        raise HTTPException(
            400,
            f"Unknown department code {dept!r} in FCR Code {code!r}. "
            f"Allowed: {', '.join(sorted(allowed_depts))}.",
        )
    return code