"""
Statistics endpoints.
"""
from __future__ import annotations

import logging
from datetime import datetime, timedelta
from typing import Optional

from fastapi import APIRouter

from app.core.config import STATUS_GROUPS
from app.core.db import db_connection, rdict
from app.services.settings import load_settings
from app.services.fcr_code import get_color, parse_date

log = logging.getLogger("fcr.router.stats")

router = APIRouter(prefix="/api", tags=["Stats"])


def cnt(conn, where: str) -> int:
    return conn.execute(f"SELECT COUNT(*) FROM fcr WHERE {where}").fetchone()[0]


@router.get("/stats")
def get_stats() -> dict:
    with db_connection() as conn:
        total = cnt(conn, "1=1")
        finished = cnt(conn, STATUS_GROUPS["finished"])
        pending = cnt(conn, STATUS_GROUPS["pending"])
        rejected = cnt(conn, STATUS_GROUPS["rejected"])
        open_count = cnt(conn, STATUS_GROUPS["open"])

        trend = [dict(r) for r in conn.execute(
            "SELECT SUBSTR(date,1,4) yr, COUNT(*) cnt FROM fcr "
            "WHERE date!='' AND SUBSTR(date,1,4) GLOB '[0-9][0-9][0-9][0-9]' "
            "GROUP BY yr ORDER BY yr"
        ).fetchall()]

        by_cat = [dict(r) for r in conn.execute(
            "SELECT category, COUNT(*) cnt FROM fcr WHERE category!='' GROUP BY category"
        ).fetchall()]

        by_ex = [dict(r) for r in conn.execute(
            "SELECT execution_status, COUNT(*) cnt FROM fcr WHERE execution_status!='' "
            "GROUP BY execution_status ORDER BY cnt DESC"
        ).fetchall()]

        by_ap = [dict(r) for r in conn.execute(
            "SELECT action_party, COUNT(*) cnt FROM fcr WHERE action_party!='' "
            "GROUP BY action_party ORDER BY cnt DESC LIMIT 10"
        ).fetchall()]

        by_level = [dict(r) for r in conn.execute(
            "SELECT level, COUNT(*) cnt FROM fcr WHERE level!='' GROUP BY level ORDER BY level"
        ).fetchall()]

        return {
            "total": total, "finished": finished, "pending": pending,
            "rejected": rejected, "open": open_count,
            "trend": trend, "by_category": by_cat,
            "by_exec_status": by_ex, "by_action_party": by_ap,
            "by_level": by_level,
        }


@router.get("/options")
def get_options() -> dict:
    with db_connection() as conn:
        def d(c: str) -> list[str]:
            return [r[0] for r in conn.execute(
                f"SELECT DISTINCT {c} FROM fcr WHERE {c}!='' ORDER BY {c}"
            ).fetchall()]
        return {
            "pedec_status": d("pedec_status"),
            "fcr_status": d("fcr_status"),
            "execution_status": d("execution_status"),
            "category": d("category"),
            "action_party": d("action_party"),
            "originator": d("originator"),
        }


@router.get("/notifications")
def get_notifications() -> dict:
    settings = load_settings()
    alert_days = int(settings.get("pedec_alert_days", 14) or 14)
    cutoff = datetime.now() - timedelta(days=alert_days)

    with db_connection() as conn:
        rows = [rdict(r) for r in conn.execute(
            "SELECT * FROM fcr WHERE date!='' "
            "AND LOWER(COALESCE(fcr_status,'')) NOT IN ('closed','rejected','cancelled','suspended')"
        ).fetchall()]

    overdue = []
    for rec in rows:
        pedec_status_text = (rec.get("pedec_status") or "").lower()
        fcr_status_text = (rec.get("fcr_status") or "").lower()
        if any(done in pedec_status_text for done in ("approved", "signed", "closed")):
            continue

        issued_to_pedec = (
            "pending" in pedec_status_text
            or "issued for afe" in fcr_status_text
            or "issued to pedec" in fcr_status_text
            or "pedec" in (rec.get("action_party") or "").lower()
            or "pedec" in (rec.get("next_action_by") or "").lower()
            or "pedec" in (rec.get("remarks") or "").lower()
        )
        dt = parse_date(rec.get("date") or "")
        if not issued_to_pedec or not dt or dt > cutoff:
            continue
        age = (datetime.now() - dt).days
        overdue.append({
            "id": rec["id"],
            "fcr_code": rec.get("fcr_code", ""),
            "fcr_subject": rec.get("fcr_subject", ""),
            "date": rec.get("date", ""),
            "age_days": age,
            "pedec_status": rec.get("pedec_status", ""),
            "fcr_status": rec.get("fcr_status", ""),
            "action_party": rec.get("action_party", ""),
        })

    overdue.sort(key=lambda r: r["age_days"], reverse=True)
    return {"count": len(overdue), "alert_days": alert_days, "items": overdue[:50]}