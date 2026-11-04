# Phase 0 — Legacy Python Technical Baseline & Inventory

> Historical migration reference for the Python edition. Python source/runtime now lives in [dibaah/fcr-python](https://github.com/dibaah/fcr-python); this file is retained to document the baseline that the .NET port was compared against.

**Scope:** Repository inspection per `masterplan.md` §10 Phase 0. No data or schema changes.
**Date:** 2026-11-01 · **App version:** 2.0.0 · **Schema version:** 2 · **Baseline tests:** 61/61 pass

---

## 1. Architecture Report

| Layer | Implementation | Location |
|---|---|---|
| Entry point | FastAPI app wiring: lifespan (`init_db`, scheduler), CORS (loopback only), security headers, static mount, router registration | `main.py` |
| Config | Version (`APP_VERSION = "2.0.0"`), paths (`BASE_DIR`/`DB_PATH`/`SETTINGS_PATH`, `FCR_DATA_DIR` env override, PyInstaller `_FROZEN`), `DEFAULT_SETTINGS`, `AREA_FOLDER_MAP`, `FCR_CODE_RE`, `STATUS_GROUPS`, `FCR_FIELDS`, Excel constants | `app/core/config.py` |
| Data access | Raw `sqlite3`, WAL mode, foreign keys on, per-request connection, idempotent `init_db()` DDL + `meta.schema_version` | `app/core/db.py` |
| Models | `FCRRecord` (19 fields + subject/code validators), `BulkSaveRequest`, `BulkDeleteRequest`, `SettingsModel` | `app/models/` |
| Routers | `health`, `fcr`, `stats`, `export`, `settings`, `backup`, `folders` — all under `/api` | `app/routers/` |
| Services | `fcr_code` (parse/validate/folder paths/`get_color`/`parse_date`), `settings` (load/save/reset/paths/payload), `export` (openpyxl workbook + charts + file export), `backup` (SQLite backup API, prune to `MAX_BACKUPS=20`), `scheduler` (60s daemon thread: auto backup + auto export) | `app/services/` |
| Frontend | Single-page vanilla JS (`script.js` ~1,295 lines), 7 pages (dashboard, records, add, notifications, reports, settings, about), login screen, en/zh i18n dicts, dark mode, Chart.js bundled locally, undo/redo stacks, column presets | `static/` |
| Desktop shell | `launcher.py`: port scan → uvicorn thread → Edge `--app=` with private profile → ctypes process monitoring → shutdown on close | `launcher.py` |
| Packaging | PyInstaller onedir (`FCR_Dashboard.spec` + `version_info.txt` + `BUILD_EXE.bat`), GitHub Actions CI (`windows-latest`) runs pytest then builds exe and publishes a release | `.github/workflows/main.yml` |
| Migration helper | Idempotent `migrate_db.py`: applies DDL, adds `LEGACY_COLUMNS`, stamps `schema_version`, reports duplicates | `migrate_db.py` |
| Server scripts | `run_server.sh` (venv + uvicorn, `--public` binds 0.0.0.0), `START_SERVER.bat` | repo root |

**Runtime model:** single-user desktop. uvicorn on `127.0.0.1:<first free port ≥8000>`, Edge app-mode window as the shell. `run_server.sh --public` exposes a LAN mode with no auth.

## 2. Verified Database Schema (inspected live)

`fcr_database.db` — 92 KB, WAL journal mode, `meta.schema_version = 2`.

**`fcr` — 22 columns, 145 rows:**
`id` INTEGER PK AUTOINCREMENT · `row_num` · `fcr_subject` (NOT NULL) · `category` · `fcr_code` · `date` · `review_date` · `originator` · `action_party` · `level` · `session_code` · `pedec_status` · `fcr_status` · `execution_status` · `closed_date` · `tr` · `next_action_by` · `arch_loc` · `last_update` · `remarks` · `created_at` · `updated_at` — all TEXT except `id`; timestamps default `datetime('now')`.

**`import_log`:** `id` PK · `filename` · `row_count` · `imported_at` — 6 rows.
**`meta`:** `key` PK · `value` — 1 row (`schema_version=2`).
**Indexes:** `idx_fcr_code`, `idx_fcr_category`, `idx_fcr_pedec_status`, `idx_fcr_fcr_status`, `idx_fcr_exec_status`, `idx_fcr_date`.

**Data observations:**
- 1 duplicate `fcr_code`: `FCR-CT-IN-031` (2 rows) — the current validator would 409 on re-save.
- Dates stored as `"YYYY-MM-DD HH:MM:SS"` text; app truncates to 10 chars on read/export.
- Value domains: `fcr_status` 15 values, `pedec_status` 7 (incl. `"Nulla"`), `execution_status` 7, `category` 2 (`Permanent`/`Temporary`).
- No `folder_path` column (derived at read time from `fcr_code` + `attachment_root`).

## 3. API Inventory (17 endpoints + docs)

| Method/Path | Behavior | Status codes |
|---|---|---|
| GET `/api/health` · `/api/about` | status, version, paths, platform, record count | 200 |
| GET `/api/fcr` | filters `search`, `pedec_status`, `fcr_status`, `exec_status`, `category`, `status_group`; `sort_by` (whitelist), `sort_order`; injects derived `folder_path` | 200, 400 unknown `status_group` |
| GET `/api/fcr/{id}` | one record + `folder_path` | 200, 404 |
| POST `/api/fcr` | validate code (400), dup → 409, blank subject → 400, auto `row_num` | 201, 400, 409 |
| PUT `/api/fcr/{id}` | full replace + `updated_at` | 200, 400, 404, 409 |
| DELETE `/api/fcr/{id}` | | 200, 404 |
| POST `/api/fcr/bulk-save` | per-record update, collects `saved`/`skipped`/`errors` | 200 |
| POST `/api/fcr/bulk-delete` | `ids` list | 200 |
| GET `/api/fcr/{id}/open-folder` · POST `.../create-folder` | resolve `attachment_root/<area>/<dept>/<code>`; open = `os.startfile`/`explorer` (Windows-only, no-op error path on Linux) | 200, 404 (errors as `{ok:false}`) |
| GET `/api/stats` | total, open/finished/pending/rejected (STATUS_GROUPS), trend-by-year, by_category, by_exec_status, by_action_party, by_level | 200 |
| GET `/api/options` | distinct values for 6 dropdown fields | 200 |
| GET `/api/notifications` | PEDEC-overdue: issued-to-PEDEC heuristics + `date` older than `pedec_alert_days` | 200 |
| GET/PUT `/api/settings` · POST `/api/settings/reset` | merge with `DEFAULT_SETTINGS`; inject derived `app_version`/`db_location`/`backup_resolved`/`auto_export_resolved`/`schema_version`; interval keys clamped ≥1; language forced en/zh | 200, 500 |
| POST `/api/backup` · GET `/api/backups` | SQLite backup API → `fcr_database_backup_<ts>.db`, prune >20 | 200, 500 |
| GET `/api/export` · `/api/export-with-charts` · POST `/api/auto-export/run` | openpyxl workbook (colored rows via `get_color`), optional `Charts & Summary` sheet (bar + 2 pie); auto-export writes file + `last_auto_export` | 200, 404 (no records) |
| GET `/` · `/favicon.ico` · `/static/*` · `/api/docs` | UI, OpenAPI | |

## 4. Feature Inventory (frontend)

7 pages: **dashboard** (4 charts, KPI counters→filtered table), **records** (inline edit, Save All/Discard, bulk delete, undo/redo, 3 column presets, filters, search, sortable), **add** (validated form, auto row number), **notifications** (PEDEC follow-ups), **reports**, **settings** (paths, intervals, dark mode, en/zh, dept codes), **about**.
**Login:** `demo`/`demo` frontend gate only — sessionStorage/localStorage flag, zero API auth.
**Response contract:** records carry derived `folder_path`; errors as `{"detail": ...}` (FastAPI) or `{ok:false, error}` (folder endpoints).

## 5. Baseline Test Results

`python -m pytest tests -q` → **61 passed** (after the Phase 0 fixes below).
Before fixes: **1 failed, 11 passed, 49 errors** — the app could not even import.
Tests use a temp DB copy via `FCR_DATA_DIR`; working DB is never touched.

## 6. Defects Found & Fixed in Phase 0

Refactor `05eb72a` dropped or broke several symbols — all restored to pre-refactor behavior:

| Defect | Fix |
|---|---|
| `app/core/db.py` imported `init_db` from `config.py` → **app could not start** | removed dead import |
| `services/export.py` imported `get_color` from `fcr_code` (never moved) + missing `Any` | restored `get_color` verbatim, added `Any` |
| `routers/stats.py` imported `parse_date` from `fcr_code` (never moved) | restored `parse_date` (also handles none/nan/nat like old `clean`) |
| `services/settings.py` missing `reset_settings` used by router | restored: delete `settings.json` → save defaults |
| `SettingsModel` `ge=1` on intervals → PUT returned **422** instead of clamped 200 | removed `ge=1` (router clamps) |
| `FCRRecord` subject validator raised `ValueError` → **422** instead of 400 | raise `HTTPException(400)` (old contract) |
| Tests imported `SETTINGS_PATH`/`generate_folder_path`/`parse_fcr_code` from `main`; version test checked `main.py` | repointed to `app.core.config` / `app.services.fcr_code` |
| Dead imports: `platform`/`Any`/`Path`/`BASE_DIR`/`_FROZEN` (main), `AREA_FOLDER_MAP`/`DEFAULT_SETTINGS`/`BASE_DIR`/`LOG_DIR`/`FCR_FIELDS`/`Path` (db.py, fcr.py) | removed |
| Docs said `APP_VERSION` in `main.py` | `AGENTS.md`/`README.md` updated to `app/core/config.py` |

## 7. Migration Risk Register

| # | Risk | Severity |
|---|---|---|
| R1 | Refactor shipped broken (no startup check); CI exists but breakage still merged | 🔴 high — add an import smoke test / branch protection before .NET work |
| R2 | API contract drift already occurred once (400→422); tests pin codes — keep them green every PR | 🔴 |
| R3 | Windows-only features: `os.startfile`/`explorer` folder ops, `X:\1-DC` paths, Edge launcher, exe packaging — no centralized equivalent | 🔴 redesign in Phase 3/8 |
| R4 | "Attachments" = external folder paths, not managed files; backups cover DB only | 🔴 new subsystem, not a port |
| R5 | SQLite single-writer + in-process 60s scheduler → unsuitable for multi-user/multi-instance | 🟠 decide provider before Phase 6 |
| R6 | No auth at all (demo gate is frontend-only); CORS loopback-only is a desktop assumption | 🟠 Phase 4 must not leak it |
| R7 | `validate_fcr_code` checks depts against `DEFAULT_SETTINGS["dept_codes"]`, not user's editable settings — adding a dept in UI won't validate in a code | 🟡 quirk to fix or preserve deliberately |
| R8 | Duplicated helpers: `clean`/`clean_date` in fcr router + export service, `parse_date` in scheduler + fcr_code; `get_color` vs `STATUS_GROUPS` encode overlapping status logic | 🟡 consolidate in .NET domain layer |
| R9 | Data quality: dup code `FCR-CT-IN-031`, `"Nulla"` pedec value, `" 00:00:00"` date suffixes — normalize during migration | 🟡 |
| R10 | Two version literals (`app/__init__.py.__version__` + `config.APP_VERSION`); `settings.json` mixes template and runtime state (`last_auto_*`) | 🟡 |
| R11 | `export.py` service imports `get_stats` from `routers/stats.py` — service→router layering inversion | 🟡 structural fix in .NET |
| R12 | `import_log` table has no API consumer; `run_server.sh` defaults APP_DIR to `/root/FCR dashboard` | 🟢 cosmetic |

## 8. Open Decisions for the Next Phase

- Add a startup/import smoke test + branch protection on `main` before Phase 1.
- Decide whether `parse_date`/`clean` consolidation happens during Phase 1 domain modeling.
- Decide whether the `dept_codes` validation quirk (R7) is preserved or fixed in .NET.
