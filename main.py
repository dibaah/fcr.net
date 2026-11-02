"""
FCR Database Dashboard — Backend API
Entry point that wires together the modular application.
"""
from __future__ import annotations

import logging
import os
from contextlib import asynccontextmanager
from typing import Iterator

from fastapi import FastAPI, HTTPException, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles

from app.core.config import (
    APP_VERSION, APP_NAME, STATIC_DIR, DB_PATH, LOG_DIR,
)
from app.core.db import init_db
from app.services.scheduler import start_scheduler, stop_scheduler
from app.routers import fcr, stats, export, settings, backup, folders, health, auth

# ─────────────────────────────────────────────────────────────────
# Logging
# ─────────────────────────────────────────────────────────────────
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s — %(message)s",
    handlers=[
        logging.FileHandler(LOG_DIR / "app.log", encoding="utf-8"),
        logging.StreamHandler(),
    ],
)
log = logging.getLogger("fcr")


# ─────────────────────────────────────────────────────────────────
# Lifespan
# ─────────────────────────────────────────────────────────────────
@asynccontextmanager
async def lifespan(_app: FastAPI) -> Iterator[None]:
    init_db()
    start_scheduler()
    log.info("%s v%s started — DB: %s", APP_NAME, APP_VERSION, DB_PATH)
    try:
        yield
    finally:
        stop_scheduler()


# ─────────────────────────────────────────────────────────────────
# FastAPI app
# ─────────────────────────────────────────────────────────────────
app = FastAPI(
    title="FCR Dashboard",
    version=APP_VERSION,
    description="Facility Change Request database for SINOPEC International — YADAVARAN",
    docs_url="/api/docs",
    redoc_url=None,
    lifespan=lifespan,
)

# Local-only desktop app: accept browser calls from the loopback interface only.
app.add_middleware(
    CORSMiddleware,
    allow_origin_regex=r"^https?://(localhost|127\.0\.0\.1)(:\d+)?$",
    allow_credentials=False,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.middleware("http")
async def add_security_headers(request: Request, call_next):
    response = await call_next(request)
    response.headers.setdefault("X-Content-Type-Options", "nosniff")
    response.headers.setdefault("X-Frame-Options", "SAMEORIGIN")
    response.headers.setdefault("Referrer-Policy", "no-referrer")
    if request.url.path.startswith("/api"):
        response.headers.setdefault("Cache-Control", "no-store")
    return response


# ─────────────────────────────────────────────────────────────────
# Static files
# ─────────────────────────────────────────────────────────────────
if STATIC_DIR.exists():
    app.mount("/static", StaticFiles(directory=str(STATIC_DIR)), name="static")
else:  # pragma: no cover — only when a build is broken
    log.error("static/ directory not found at %s — the UI will not be served", STATIC_DIR)


@app.get("/", include_in_schema=False)
def serve_index():
    index = STATIC_DIR / "index.html"
    if not index.exists():
        raise HTTPException(500, f"UI bundle missing: {index} not found")
    return FileResponse(str(index))


@app.get("/favicon.ico", include_in_schema=False)
def serve_favicon():
    icon = STATIC_DIR / "icon.ico"
    if icon.exists():
        return FileResponse(str(icon))
    raise HTTPException(404, "No favicon")


# ─────────────────────────────────────────────────────────────────
# Include routers
# ─────────────────────────────────────────────────────────────────
app.include_router(auth.router)
app.include_router(health.router)
app.include_router(fcr.router)
app.include_router(stats.router)
app.include_router(export.router)
app.include_router(settings.router)
app.include_router(backup.router)
app.include_router(folders.router)


# ─────────────────────────────────────────────────────────────────
# Entry point (direct `python main.py`)
# ─────────────────────────────────────────────────────────────────
if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="127.0.0.1", port=int(os.environ.get("FCR_PORT", "8000")))