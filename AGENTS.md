# FCR Dashboard — Agent Notes

## Overview
FastAPI backend (`main.py` entry point, modular `app/` package) serving a vanilla-JS dashboard in `static/`. SQLite DB `fcr_database.db`. Desktop entry via `launcher.py` (uvicorn + Edge app mode). Portable Windows exe built with PyInstaller.

## Key files
- `main.py` — FastAPI app entry point (lifespan, middleware, router wiring).
- `app/` — backend package: `core/` (config, db), `routers/` (API endpoints), `services/` (export, backup, scheduler, settings, fcr_code), `models/` (pydantic schemas).
- `launcher.py` — desktop entry point.
- `migrate_db.py` — schema migration helper.
- `static/index.html`, `static/script.js`, `static/style.css` — UI.
- `tests/test_api.py` — API regression tests (uses temp DB copy).
- `settings.json` — default config template.

## Commands
- Install dev deps: `pip install -r requirements-dev.txt`
- Run tests: `python -m pytest tests -q`
- Run server (Linux): `./run_server.sh` (or `--public`, `--port`)
- Run server (Windows): `START_SERVER.bat`

## Conventions
- Version source of truth: `APP_VERSION` in `app/core/config.py`.
- FCR code format: `FCR-<AREA>-<DEPT>-<NNN>`.
- Settings persisted in `settings.json`.
- No comments unless requested.
- Keep changes minimal and compatible with existing style.
