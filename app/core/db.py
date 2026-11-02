"""
Database connection and schema initialization.
"""
from __future__ import annotations

import logging
import sqlite3
from contextlib import contextmanager

from app.core.config import DB_PATH, SCHEMA_VERSION

log = logging.getLogger("fcr.db")


def get_db() -> sqlite3.Connection:
    """Create a new SQLite connection with WAL mode and row factory."""
    try:
        conn = sqlite3.connect(str(DB_PATH), timeout=10)
        conn.row_factory = sqlite3.Row
        conn.execute("PRAGMA journal_mode=WAL")
        conn.execute("PRAGMA foreign_keys=ON")
        return conn
    except sqlite3.Error as exc:
        log.error("Database connection failed: %s", exc)
        raise


@contextmanager
def db_connection() -> sqlite3.Connection:
    """Context manager for database connections."""
    conn = get_db()
    try:
        yield conn
    finally:
        conn.close()


def init_db() -> None:
    """Create the schema when missing. Idempotent and safe to run on old databases."""
    conn = get_db()
    try:
        conn.executescript("""
            CREATE TABLE IF NOT EXISTS fcr (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                row_num          TEXT DEFAULT '',
                fcr_subject      TEXT NOT NULL DEFAULT '',
                category         TEXT DEFAULT '',
                fcr_code         TEXT DEFAULT '',
                date             TEXT DEFAULT '',
                review_date      TEXT DEFAULT '',
                originator       TEXT DEFAULT '',
                action_party     TEXT DEFAULT '',
                level            TEXT DEFAULT '',
                session_code     TEXT DEFAULT '',
                pedec_status     TEXT DEFAULT '',
                fcr_status       TEXT DEFAULT '',
                execution_status TEXT DEFAULT '',
                closed_date      TEXT DEFAULT '',
                tr               TEXT DEFAULT '',
                next_action_by   TEXT DEFAULT '',
                arch_loc         TEXT DEFAULT '',
                last_update      TEXT DEFAULT '',
                remarks          TEXT DEFAULT '',
                created_at       TEXT DEFAULT (datetime('now')),
                updated_at       TEXT DEFAULT (datetime('now'))
            );
            CREATE TABLE IF NOT EXISTS import_log (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                filename    TEXT,
                row_count   INTEGER,
                imported_at TEXT DEFAULT (datetime('now'))
            );
            CREATE TABLE IF NOT EXISTS meta (
                key   TEXT PRIMARY KEY,
                value TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_fcr_code ON fcr(fcr_code);
            CREATE INDEX IF NOT EXISTS idx_fcr_category ON fcr(category);
            CREATE INDEX IF NOT EXISTS idx_fcr_pedec_status ON fcr(pedec_status);
            CREATE INDEX IF NOT EXISTS idx_fcr_fcr_status ON fcr(fcr_status);
            CREATE INDEX IF NOT EXISTS idx_fcr_exec_status ON fcr(execution_status);
            CREATE INDEX IF NOT EXISTS idx_fcr_date ON fcr(date);
        """)
        conn.execute(
            "INSERT INTO meta(key, value) VALUES('schema_version', ?) "
            "ON CONFLICT(key) DO UPDATE SET value=excluded.value",
            (str(SCHEMA_VERSION),),
        )
        conn.commit()
        log.info("Database ready (schema v%s): %s", SCHEMA_VERSION, DB_PATH)
    finally:
        conn.close()


def rdict(row: sqlite3.Row) -> dict:
    return dict(row)