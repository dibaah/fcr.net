"""
Regression tests for the FCR Dashboard backend.

Run from the repository root:

    python -m pytest tests -q

The suite never touches the working copy of ``fcr_database.db``: the fixture
copies it into a temporary directory and points the app at that copy via the
``FCR_DATA_DIR`` environment variable.
"""
from __future__ import annotations

import io
import os
import re
import shutil
import sys
import tempfile
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[1]


# ─────────────────────────────────────────────────────────────────
# Static checks for files the API tests never import
# ─────────────────────────────────────────────────────────────────
@pytest.mark.parametrize("filename", ["main.py", "launcher.py", "migrate_db.py",
                                      "FCR_Dashboard.spec"])
def test_python_files_parse(filename):
    import ast

    path = REPO_ROOT / filename
    assert path.exists(), f"{filename} is missing"
    ast.parse(path.read_text(encoding="utf-8"), filename=str(path))


def test_version_is_consistent_everywhere():
    """One version number for the whole release."""
    version = "2.0.0"
    assert f'APP_VERSION = "{version}"' in (REPO_ROOT / "app" / "core" / "config.py").read_text(encoding="utf-8")
    assert version in (REPO_ROOT / "README.md").read_text(encoding="utf-8")
    for name in ("version_info.txt", "BUILD_EXE.bat"):
        text = (REPO_ROOT / name).read_text(encoding="utf-8", errors="replace")
        assert version in text, f"{name} does not mention {version}"
    assert "2.0.0" in (REPO_ROOT / "static" / "index.html").read_text(encoding="utf-8")


def test_no_stale_webview_or_old_version_references():
    """The app runs in Edge app mode; pywebview and 1.3.1 are gone."""
    for name in ("README.md", "static/index.html"):
        text = (REPO_ROOT / name).read_text(encoding="utf-8")
        assert "PyWebView" not in text, f"{name} still mentions PyWebView"
        assert "1.3.1" not in text, f"{name} still mentions the old 1.3.1 version"


def test_launcher_never_opens_a_console_window():
    """A windowed build has no console, so every child process must be silent.

    Regression test for the console window that flashed on start: the launcher
    used to poll PowerShell without CREATE_NO_WINDOW.
    """
    text = (REPO_ROOT / "launcher.py").read_text(encoding="utf-8")
    assert "_NO_WINDOW" in text, "launcher.py must define CREATE_NO_WINDOW"
    assert "creationflags=_NO_WINDOW" in text, "child processes must use _NO_WINDOW"

    # No bare subprocess.run / Popen call that could pop up a console.
    for match in re.finditer(r"subprocess\.(?:run|Popen)\(", text):
        snippet = text[match.end():match.end() + 400]
        assert ("_run_hidden" in snippet or "_NO_WINDOW" in snippet), (
            f"subprocess call near offset {match.start()} does not hide its console"
        )

    # The Edge polling must not shell out at all (it used to call powershell).
    assert "_count_processes" in text, "process polling should use ctypes, not a subprocess"


def test_spec_builds_a_windowed_exe():
    spec = (REPO_ROOT / "FCR_Dashboard.spec").read_text(encoding="utf-8")
    assert "console=False" in spec, "the exe must be built without a console window"


# ─────────────────────────────────────────────────────────────────
# Sign-in screen (demo gate in front of the dashboard)
# ─────────────────────────────────────────────────────────────────
def test_login_screen_has_username_and_password_fields():
    html = (REPO_ROOT / "static" / "index.html").read_text(encoding="utf-8")
    assert 'id="login-screen"' in html
    assert 'id="login-form"' in html
    assert 'id="login-user"' in html
    assert 'id="login-pass"' in html
    assert 'id="login-error"' in html
    assert 'type="password"' in html


def test_login_screen_is_opaque():
    """The dashboard must not be visible behind the sign-in overlay."""
    css = (REPO_ROOT / "static" / "style.css").read_text(encoding="utf-8")
    login = next(
        line for line in css.splitlines() if line.startswith(".login-screen{")
    )
    # A translucent background (rgba/hsla with alpha < 1) would leak the app.
    assert "rgba(" not in login, f"login overlay must be opaque: {login}"
    assert "opacity" not in login, f"login overlay must be opaque: {login}"


def test_login_wired_to_backend_auth():
    js = (REPO_ROOT / "static" / "script.js").read_text(encoding="utf-8")
    assert "'/api/auth/login'" in js
    assert "'/api/auth/me'" in js
    assert "hideLoginScreen()" in js


def test_signed_in_session_skips_the_login_screen():
    js = (REPO_ROOT / "static" / "script.js").read_text(encoding="utf-8")
    assert "if (!await isAuthenticated())" in js, "a valid session should skip the gate"


def test_python_auth_shim_demo_login(client):
    c = client
    bad = c.post("/api/auth/login", json={"username": "demo", "password": "nope"})
    assert bad.status_code == 401
    ok = c.post("/api/auth/login", json={"username": "demo", "password": "demo"})
    assert ok.status_code == 200
    assert c.get("/api/auth/me").status_code == 200
    c.post("/api/auth/logout")
    assert c.get("/api/auth/me").status_code == 401


@pytest.fixture(scope="session")
def client():
    """TestClient bound to a throw-away copy of the database."""
    tmp_dir = Path(tempfile.mkdtemp(prefix="fcr-test-"))
    source_db = REPO_ROOT / "fcr_database.db"
    if source_db.exists():
        shutil.copy2(source_db, tmp_dir / "fcr_database.db")

    os.environ["FCR_DATA_DIR"] = str(tmp_dir)
    if str(REPO_ROOT) not in sys.path:
        sys.path.insert(0, str(REPO_ROOT))

    import main  # imported *after* FCR_DATA_DIR is set
    from fastapi.testclient import TestClient

    assert main.DB_PATH.parent == tmp_dir.resolve(), "tests must not use the real database"

    with TestClient(main.app) as test_client:
        yield test_client

    shutil.rmtree(tmp_dir, ignore_errors=True)


# ─────────────────────────────────────────────────────────────────
# Health, UI shell, assets
# ─────────────────────────────────────────────────────────────────
def test_health(client):
    r = client.get("/api/health")
    assert r.status_code == 200, r.text
    body = r.json()
    assert body["status"] == "ok"
    assert body["version"] == "2.0.0"
    assert body["db"].endswith("fcr_database.db")


def test_index_serves_ui(client):
    r = client.get("/")
    assert r.status_code == 200, r.text
    assert "FCR Dashboard" in r.text
    assert "/static/script.js" in r.text


@pytest.mark.parametrize("asset", ["script.js", "style.css", "chart.min.js"])
def test_static_assets(client, asset):
    r = client.get(f"/static/{asset}")
    assert r.status_code == 200, f"{asset} -> {r.status_code}"
    assert len(r.content) > 100


def test_security_headers(client):
    r = client.get("/api/health")
    assert r.headers.get("x-content-type-options") == "nosniff"
    assert r.headers.get("x-frame-options") == "SAMEORIGIN"
    assert r.headers.get("cache-control") == "no-store"


def test_about(client):
    body = client.get("/api/about").json()
    assert body["version"] == "2.0.0"
    assert body["records"] > 0
    assert body["schema_version"] >= 2


def test_openapi_documents_every_route(client):
    paths = client.get("/openapi.json").json()["paths"]
    for route in ("/api/fcr", "/api/fcr/{fcr_id}", "/api/stats", "/api/settings",
                  "/api/notifications", "/api/backup", "/api/export"):
        assert route in paths, f"{route} missing from OpenAPI schema"


# ─────────────────────────────────────────────────────────────────
# Records: read, filter, sort
# ─────────────────────────────────────────────────────────────────
def test_list_records(client):
    rows = client.get("/api/fcr").json()
    assert rows, "seed database should contain records"
    assert "folder_path" in rows[0]
    assert "id" in rows[0]


def test_filters(client):
    rows = client.get("/api/fcr", params={"pedec_status": "Closed"}).json()
    assert rows
    assert all(r["pedec_status"] == "Closed" for r in rows)

    hits = client.get("/api/fcr", params={"search": "valve"}).json()
    assert isinstance(hits, list)


@pytest.mark.parametrize("field", ["date", "fcr_code", "fcr_subject", "category",
                                   "originator", "pedec_status", "execution_status"])
@pytest.mark.parametrize("order", ["asc", "desc"])
def test_sorting(client, field, order):
    r = client.get("/api/fcr", params={"sort_by": field, "sort_order": order})
    assert r.status_code == 200, r.text


def test_sort_field_is_whitelisted(client):
    """A hostile sort_by must be ignored, not injected into SQL."""
    r = client.get("/api/fcr", params={"sort_by": "id; DROP TABLE fcr;--"})
    assert r.status_code == 200, r.text
    assert client.get("/api/fcr").json(), "table must still exist"


# ─────────────────────────────────────────────────────────────────
# Stats ↔ filters must agree (regression: quick-stat buttons showed 1 row)
# ─────────────────────────────────────────────────────────────────
def test_status_group_matches_stats(client):
    stats = client.get("/api/stats").json()
    for group in ("open", "finished", "pending", "rejected"):
        rows = client.get("/api/fcr", params={"status_group": group}).json()
        assert len(rows) == stats[group], (
            f"status_group={group}: filter returned {len(rows)} rows "
            f"but /api/stats reports {stats[group]}"
        )


def test_unknown_status_group_rejected(client):
    r = client.get("/api/fcr", params={"status_group": "nonsense"})
    assert r.status_code == 400, r.text


def test_status_groups_partition_the_total(client):
    stats = client.get("/api/stats").json()
    assert stats["open"] + stats["finished"] + stats["rejected"] == stats["total"], (
        "open/finished/rejected must be mutually exclusive and cover every record"
    )
    assert stats["pending"] <= stats["open"], "in-progress rows are also open"


def test_stats_shape(client):
    stats = client.get("/api/stats").json()
    for key in ("total", "open", "finished", "pending", "rejected",
                "trend", "by_category", "by_exec_status", "by_action_party", "by_level"):
        assert key in stats, f"missing key {key}"
    assert stats["total"] >= 1


# ─────────────────────────────────────────────────────────────────
# CRUD
# ─────────────────────────────────────────────────────────────────
def test_crud_lifecycle(client):
    payload = {
        "fcr_subject": "PYTEST — safe to delete",
        "fcr_code": "FCR-CT-PI-997",
        "category": "Temporary",
        "date": "2026-01-15",
        "pedec_status": "Closed",
        "fcr_status": "Closed",
        "execution_status": "Finished",
    }
    created = client.post("/api/fcr", json=payload)
    assert created.status_code == 201, created.text
    record = created.json()
    new_id = record["id"]
    assert record["fcr_code"] == "FCR-CT-PI-997"
    assert record["row_num"], "row_num should be auto-assigned"
    assert record["folder_path"].endswith("CTEP\\PI\\FCR-CT-PI-997") or \
        record["folder_path"].endswith("CTEP/PI/FCR-CT-PI-997")

    assert client.get(f"/api/fcr/{new_id}").status_code == 200

    duplicate = client.post("/api/fcr", json=payload)
    assert duplicate.status_code == 409, duplicate.text

    payload["fcr_subject"] = "PYTEST — updated"
    updated = client.put(f"/api/fcr/{new_id}", json=payload)
    assert updated.status_code == 200, updated.text
    assert updated.json()["fcr_subject"] == "PYTEST — updated"

    bulk = client.post("/api/fcr/bulk-save",
                       json={"records": [{**payload, "id": new_id, "remarks": "bulk"}]})
    assert bulk.status_code == 200, bulk.text
    assert bulk.json()["saved"] == 1
    assert bulk.json()["errors"] == []

    assert client.delete(f"/api/fcr/{new_id}").status_code == 200
    assert client.get(f"/api/fcr/{new_id}").status_code == 404


def test_subject_is_required(client):
    r = client.post("/api/fcr", json={"fcr_subject": "   ", "fcr_code": "FCR-CT-PI-996"})
    assert r.status_code == 400, r.text


@pytest.mark.parametrize("bad_code", [
    "FCR-CT-PI",          # too few segments
    "CT-PI-046",          # missing prefix
    "FCR-ZZ-PI-046",      # unknown area
    "FCR-CT-QQ-046",      # unknown department
    "FCR-CT-PI-abc",      # non-numeric index
])
def test_invalid_fcr_code_rejected(client, bad_code):
    r = client.post("/api/fcr", json={"fcr_subject": "bad code", "fcr_code": bad_code})
    assert r.status_code == 400, f"{bad_code} should be rejected, got {r.status_code}"


def test_fcr_code_is_normalised(client):
    r = client.post("/api/fcr", json={"fcr_subject": "PYTEST normalise",
                                      "fcr_code": "  fcr-ct-pi-995 "})
    assert r.status_code == 201, r.text
    record = r.json()
    assert record["fcr_code"] == "FCR-CT-PI-995"
    client.delete(f"/api/fcr/{record['id']}")


def test_bulk_delete(client):
    ids = []
    for index in (401, 402):
        r = client.post("/api/fcr", json={"fcr_subject": f"PYTEST delete {index}",
                                          "fcr_code": f"FCR-CT-PI-{index}"})
        assert r.status_code == 201, r.text
        ids.append(r.json()["id"])
    r = client.post("/api/fcr/bulk-delete", json={"ids": ids})
    assert r.status_code == 200, r.text
    assert r.json()["deleted"] == 2
    for record_id in ids:
        assert client.get(f"/api/fcr/{record_id}").status_code == 404


def test_missing_record_returns_404(client):
    assert client.get("/api/fcr/999999").status_code == 404
    assert client.delete("/api/fcr/999999").status_code == 404


# ─────────────────────────────────────────────────────────────────
# Settings
# ─────────────────────────────────────────────────────────────────
def test_settings_roundtrip(client):
    original = client.get("/api/settings").json()
    assert original["app_version"] == "2.0.0"
    assert "auto_backup_enabled" in original

    updated = client.put("/api/settings", json={"pedec_alert_days": 21})
    assert updated.status_code == 200, updated.text
    assert updated.json()["pedec_alert_days"] == 21
    assert client.get("/api/settings").json()["pedec_alert_days"] == 21

    # interval values are clamped to >= 1
    clamped = client.put("/api/settings", json={"auto_export_interval_days": 0})
    assert clamped.json()["auto_export_interval_days"] == 1

    client.put("/api/settings", json={
        "pedec_alert_days": original.get("pedec_alert_days", 14),
        "auto_export_interval_days": original.get("auto_export_interval_days", 2),
    })


def test_derived_settings_are_not_persisted(client):
    from app.core.config import SETTINGS_PATH
    import json

    client.get("/api/settings")
    stored = json.loads(SETTINGS_PATH.read_text(encoding="utf-8"))
    for key in ("db_location", "backup_resolved", "app_version", "schema_version"):
        assert key not in stored, f"{key} must not be written to settings.json"


# ─────────────────────────────────────────────────────────────────
# Notifications, backups, exports, folders
# ─────────────────────────────────────────────────────────────────
def test_notifications(client):
    body = client.get("/api/notifications").json()
    assert set(body) >= {"count", "alert_days", "items"}
    assert body["count"] == len(body["items"]) or body["count"] >= len(body["items"])
    for item in body["items"]:
        assert item["age_days"] >= body["alert_days"]


def test_backup_and_listing(client):
    before = len(client.get("/api/backups").json())
    created = client.post("/api/backup")
    assert created.status_code == 200, created.text
    body = created.json()
    assert body["ok"] is True
    assert Path(body["path"]).exists()
    assert len(client.get("/api/backups").json()) == before + 1


def test_backup_file_is_a_readable_database(client):
    import sqlite3

    path = client.post("/api/backup").json()["path"]
    conn = sqlite3.connect(path)
    try:
        assert conn.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
        assert conn.execute("SELECT COUNT(*) FROM fcr").fetchone()[0] >= 1
    finally:
        conn.close()


def _load_first_sheet(content: bytes):
    import openpyxl

    return openpyxl.load_workbook(io.BytesIO(content))


def test_export_workbook(client):
    r = client.get("/api/export")
    assert r.status_code == 200, r.text
    assert r.content[:2] == b"PK"
    wb = _load_first_sheet(r.content)
    ws = wb.active
    assert ws.title == "FCR Database"
    assert ws.cell(row=1, column=2).value == "FCR Subject"

    # dates are normalised to YYYY-MM-DD (regression: they used to keep " 00:00:00")
    date_col = [c.value for c in ws["E"]][1:]
    for value in date_col:
        if value:
            assert len(str(value)) == 10, f"unclean date in export: {value!r}"


def test_export_with_charts(client):
    r = client.get("/api/export-with-charts")
    assert r.status_code == 200, r.text
    assert r.content[:2] == b"PK"
    wb = _load_first_sheet(r.content)
    assert "Charts & Summary" in wb.sheetnames
    assert len(wb["Charts & Summary"]._charts) >= 2


def test_auto_export_to_file(client):
    r = client.post("/api/auto-export/run")
    assert r.status_code == 200, r.text
    body = r.json()
    assert body["ok"] is True, body
    assert Path(body["path"]).exists()


def test_folder_helpers(client):
    from app.services.fcr_code import generate_folder_path, parse_fcr_code

    assert parse_fcr_code("FCR-CT-PI-046") == ("CTEP", "PI")
    assert parse_fcr_code("FCR-WL-IN-002") == ("WELLS", "IN")
    assert parse_fcr_code("") == ("", "")
    assert generate_folder_path(r"X:\1-DC", "FCR-CT-PI-046").endswith(
        os.path.join("CTEP", "PI", "FCR-CT-PI-046")
    )
    assert generate_folder_path(r"X:\1-DC", "garbage") == ""


def test_open_folder_reports_missing_path_without_crashing(client):
    record = next(r for r in client.get("/api/fcr").json() if r["fcr_code"])
    response = client.get(f"/api/fcr/{record['id']}/open-folder")
    assert response.status_code == 200, response.text
    body = response.json()
    assert "ok" in body
    if not body["ok"]:
        assert "error" in body
