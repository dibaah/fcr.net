"""
FCR CRUD endpoints.
"""
from __future__ import annotations

import logging
from typing import Any, Optional

from fastapi import APIRouter, HTTPException

from app.core.config import (
    FCR_FIELDS, STATUS_GROUPS,
)
from app.core.db import db_connection, rdict
from app.models import FCRRecord, BulkSaveRequest, BulkDeleteRequest
from app.services.fcr_code import validate_fcr_code, generate_folder_path
from app.services.settings import load_settings

log = logging.getLogger("fcr.router.fcr")

router = APIRouter(prefix="/api", tags=["FCR"])


def clean(v: Any) -> str:
    if v is None:
        return ""
    s = str(v).strip()
    return "" if s.lower() in ("none", "nan", "nat") else s


def clean_date(v: Any) -> str:
    s = clean(v)
    return s[:10] if len(s) >= 10 and __import__("re").match(r"^\d{4}-\d{2}-\d{2}", s) else s


def dup_code(conn, code: str, excl: int = 0) -> bool:
    if not code:
        return False
    return bool(conn.execute(
        "SELECT id FROM fcr WHERE LOWER(fcr_code)=LOWER(?) AND id!=?",
        (code.strip(), excl),
    ).fetchone())


def record_values(record: FCRRecord) -> list[str]:
    """Serialize a model to FCR_FIELDS order, normalizing the code and dates."""
    values: list[str] = []
    date_fields = ("date", "review_date", "closed_date", "last_update")
    for field in FCR_FIELDS:
        value = getattr(record, field)
        if field == "fcr_code":
            values.append(validate_fcr_code(value))
        elif field in date_fields:
            values.append(clean_date(value))
        else:
            values.append(clean(value))
    return values


def with_folder_path(rec: dict) -> dict:
    settings = load_settings()
    parent_path = settings.get("attachment_root", r"X:\1-DC")
    fcr_code = (rec.get("fcr_code") or "").strip()
    rec["folder_path"] = generate_folder_path(parent_path, fcr_code)
    return rec


@router.get("/fcr")
def list_fcr(
    search: Optional[str] = None,
    pedec_status: Optional[str] = None,
    fcr_status: Optional[str] = None,
    exec_status: Optional[str] = None,
    category: Optional[str] = None,
    status_group: Optional[str] = None,
    sort_by: str = "date",
    sort_order: str = "desc",
) -> list[dict]:
    with db_connection() as conn:
        q = "SELECT * FROM fcr WHERE 1=1"
        params: list[Any] = []
        if pedec_status:
            q += " AND pedec_status=?"
            params.append(pedec_status)
        if fcr_status:
            q += " AND fcr_status=?"
            params.append(fcr_status)
        if exec_status:
            q += " AND execution_status=?"
            params.append(exec_status)
        if category:
            q += " AND category=?"
            params.append(category)
        if status_group:
            condition = STATUS_GROUPS.get(status_group.lower())
            if condition is None:
                raise HTTPException(
                    400,
                    f"Unknown status_group {status_group!r}. "
                    f"Allowed: {', '.join(sorted(STATUS_GROUPS))}.",
                )
            q += f" AND ({condition})"
        if search:
            t = f"%{search}%"
            q += (" AND (fcr_code LIKE ? OR fcr_subject LIKE ? OR originator LIKE ? "
                  "OR action_party LIKE ? OR remarks LIKE ? OR pedec_status LIKE ? "
                  "OR fcr_status LIKE ? OR execution_status LIKE ?)")
            params.extend([t] * 8)

        allowed_sort = {"date", "fcr_code", "fcr_subject", "category",
                        "originator", "action_party", "pedec_status",
                        "fcr_status", "execution_status", "row_num"}
        sort_field = sort_by if sort_by in allowed_sort else "date"
        order = "DESC" if sort_order.lower() == "desc" else "ASC"

        if sort_field == "date":
            q += f" ORDER BY CASE WHEN {sort_field}='' THEN 1 ELSE 0 END, {sort_field} {order}"
        elif sort_field == "row_num":
            q += f" ORDER BY CAST({sort_field} AS INTEGER) {order}"
        else:
            q += f" ORDER BY {sort_field} {order}"

        return [with_folder_path(rdict(r)) for r in conn.execute(q, params).fetchall()]


@router.get("/fcr/{fcr_id}")
def get_fcr(fcr_id: int) -> dict:
    with db_connection() as conn:
        row = conn.execute("SELECT * FROM fcr WHERE id=?", (fcr_id,)).fetchone()
        if not row:
            raise HTTPException(404, "FCR not found")
        return with_folder_path(rdict(row))


@router.post("/fcr", status_code=201)
def create_fcr(record: FCRRecord) -> dict:
    with db_connection() as conn:
        code = validate_fcr_code(record.fcr_code)
        if dup_code(conn, code):
            raise HTTPException(409, f"FCR Code '{code}' already exists")
        if not clean(record.row_num):
            # Auto-number
            row = conn.execute(
                "SELECT MAX(CAST(row_num AS INTEGER)) FROM fcr WHERE row_num GLOB '[0-9]*'"
            ).fetchone()
            record.row_num = str(int(row[0] or 0) + 1)

        vals = record_values(record)
        cur = conn.execute(
            f"INSERT INTO fcr ({','.join(FCR_FIELDS)}) VALUES ({','.join(['?'] * len(FCR_FIELDS))})",
            vals,
        )
        conn.commit()
        row = conn.execute("SELECT * FROM fcr WHERE id=?", (cur.lastrowid,)).fetchone()
        log.info("Created FCR id=%s code=%s", cur.lastrowid, code)
        return with_folder_path(rdict(row))


@router.put("/fcr/{fcr_id}")
def update_fcr(fcr_id: int, record: FCRRecord) -> dict:
    with db_connection() as conn:
        if not conn.execute("SELECT id FROM fcr WHERE id=?", (fcr_id,)).fetchone():
            raise HTTPException(404, "FCR not found")
        code = validate_fcr_code(record.fcr_code)
        if dup_code(conn, code, excl=fcr_id):
            raise HTTPException(409, f"FCR Code '{code}' already exists")

        sp = [f"{f}=?" for f in FCR_FIELDS] + ["updated_at=datetime('now')"]
        vals = record_values(record) + [fcr_id]
        conn.execute(f"UPDATE fcr SET {','.join(sp)} WHERE id=?", vals)
        conn.commit()
        log.info("Updated FCR id=%s", fcr_id)
        return with_folder_path(rdict(conn.execute("SELECT * FROM fcr WHERE id=?", (fcr_id,)).fetchone()))


@router.delete("/fcr/{fcr_id}")
def delete_fcr(fcr_id: int) -> dict:
    with db_connection() as conn:
        row = conn.execute("SELECT fcr_code FROM fcr WHERE id=?", (fcr_id,)).fetchone()
        if not row:
            raise HTTPException(404, "FCR not found")
        conn.execute("DELETE FROM fcr WHERE id=?", (fcr_id,))
        conn.commit()
        log.info("Deleted FCR id=%s code=%s", fcr_id, row["fcr_code"])
        return {"deleted": fcr_id}


@router.post("/fcr/bulk-save")
def bulk_save(req: BulkSaveRequest) -> dict:
    with db_connection() as conn:
        sp = [f"{f}=?" for f in FCR_FIELDS] + ["updated_at=datetime('now')"]
        saved, skipped = 0, 0
        errors: list[str] = []
        for rec in req.records:
            if rec.id is None:
                continue
            if not conn.execute("SELECT id FROM fcr WHERE id=?", (rec.id,)).fetchone():
                skipped += 1
                errors.append(f"id {rec.id}: not found")
                continue
            try:
                code = validate_fcr_code(rec.fcr_code)
            except HTTPException as exc:
                skipped += 1
                errors.append(f"id {rec.id}: {exc.detail}")
                continue
            if dup_code(conn, code, excl=rec.id):
                skipped += 1
                errors.append(f"id {rec.id}: duplicate FCR Code {code}")
                continue
            if not clean(rec.fcr_subject):
                skipped += 1
                errors.append(f"id {rec.id}: FCR Subject required")
                continue
            vals = record_values(rec) + [rec.id]
            conn.execute(f"UPDATE fcr SET {','.join(sp)} WHERE id=?", vals)
            saved += 1
        conn.commit()
        log.info("Bulk saved %d records (%d skipped)", saved, skipped)
        return {"saved": saved, "skipped": skipped, "errors": errors}


@router.post("/fcr/bulk-delete")
def bulk_delete(req: BulkDeleteRequest) -> dict:
    if not req.ids:
        return {"deleted": 0}
    with db_connection() as conn:
        placeholders = ",".join(["?"] * len(req.ids))
        cur = conn.execute(f"DELETE FROM fcr WHERE id IN ({placeholders})", req.ids)
        conn.commit()
        log.info("Bulk deleted %d records", cur.rowcount)
        return {"deleted": cur.rowcount}