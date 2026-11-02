"""
FCR Dashboard — database migration / repair script.

Brings an existing ``fcr_database.db`` up to the current schema (v2) without
touching any data. It is idempotent: running it twice changes nothing.

Usage:
    python migrate_db.py                (uses the database next to this file)
    python migrate_db.py "D:\\path\\fcr_database.db"
    set FCR_DATA_DIR=D:\\data        (same override the app honours)

The schema here must stay in sync with ``init_db()`` in main.py.
"""
from __future__ import annotations

import os
import sqlite3
import sys
from pathlib import Path

SCHEMA_VERSION = 2

DDL = """
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
"""

# Columns added over time: (column, DDL fragment). Missing ones are added.
LEGACY_COLUMNS = {
    "row_num": "TEXT DEFAULT ''",
    "category": "TEXT DEFAULT ''",
    "level": "TEXT DEFAULT ''",
    "session_code": "TEXT DEFAULT ''",
    "pedec_status": "TEXT DEFAULT ''",
    "fcr_status": "TEXT DEFAULT ''",
    "execution_status": "TEXT DEFAULT ''",
    "closed_date": "TEXT DEFAULT ''",
    "tr": "TEXT DEFAULT ''",
    "next_action_by": "TEXT DEFAULT ''",
    "arch_loc": "TEXT DEFAULT ''",
    "last_update": "TEXT DEFAULT ''",
    "remarks": "TEXT DEFAULT ''",
    "created_at": "TEXT DEFAULT (datetime('now'))",
    "updated_at": "TEXT DEFAULT (datetime('now'))",
}


def default_db_path() -> Path:
    override = os.environ.get("FCR_DATA_DIR", "").strip()
    if override:
        return Path(override).expanduser() / "fcr_database.db"
    if getattr(sys, "frozen", False):
        return Path(sys.executable).parent / "fcr_database.db"
    return Path(__file__).resolve().parent / "fcr_database.db"


def migrate(db_path: Path) -> bool:
    """Apply the current schema. Returns True when the database was changed."""
    if not db_path.exists():
        print(f"[INFO] Database not found: {db_path}")
        print("[INFO] A new one will be created automatically on first run.")
        return False

    print(f"[INFO] Database: {db_path}")
    conn = sqlite3.connect(str(db_path))
    changed = False
    try:
        existing_tables = {
            r[0] for r in conn.execute(
                "SELECT name FROM sqlite_master WHERE type='table'"
            )
        }

        conn.executescript(DDL)

        # Add any columns a very old build might be missing.
        columns = {r[1] for r in conn.execute("PRAGMA table_info(fcr)")}
        for column, ddl in LEGACY_COLUMNS.items():
            if column not in columns:
                conn.execute(f"ALTER TABLE fcr ADD COLUMN {column} {ddl}")
                print(f"[OK] Added column: {column}")
                changed = True

        # `folder_path` used to be stored; it is now derived from the FCR code
        # on every request, so a stale column is harmless but confusing.
        if "folder_path" in columns:
            print("[NOTE] Legacy 'folder_path' column present — it is ignored "
                  "(folder paths are computed from the FCR code now).")

        if "meta" not in existing_tables:
            changed = True

        row = conn.execute(
            "SELECT value FROM meta WHERE key='schema_version'"
        ).fetchone()
        previous = int(row[0]) if row and str(row[0]).isdigit() else 0
        conn.execute(
            "INSERT INTO meta(key, value) VALUES('schema_version', ?) "
            "ON CONFLICT(key) DO UPDATE SET value=excluded.value",
            (str(SCHEMA_VERSION),),
        )
        conn.commit()

        total = conn.execute("SELECT COUNT(*) FROM fcr").fetchone()[0]
        duplicates = conn.execute(
            "SELECT LOWER(fcr_code) FROM fcr WHERE fcr_code!='' "
            "GROUP BY LOWER(fcr_code) HAVING COUNT(*) > 1"
        ).fetchall()

        if previous != SCHEMA_VERSION:
            print(f"[OK] Schema version {previous} -> {SCHEMA_VERSION}")
            changed = True

        print(f"[OK] Records: {total}")
        if duplicates:
            codes = ", ".join(r[0] for r in duplicates[:10])
            print(f"[WARN] Duplicate FCR codes found (kept as-is): {codes}")
        print("[OK] Migration complete." if changed else "[OK] Already up to date.")
        return changed
    finally:
        conn.close()


def main() -> int:
    print("=" * 60)
    print("  FCR Dashboard — Database Migration (schema v2)")
    print("=" * 60)
    path = Path(sys.argv[1]).expanduser() if len(sys.argv) > 1 else default_db_path()
    try:
        migrate(path)
    except Exception as exc:
        print(f"[ERROR] Migration failed: {exc}")
        return 1
    return 0


if __name__ == "__main__":
    code = main()
    if sys.stdin and sys.stdin.isatty():
        input("Press Enter to exit...")
    sys.exit(code)
