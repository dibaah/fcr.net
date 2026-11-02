"""
Development auth shim for the legacy Python app.

The real security boundary lives in the .NET host (cookie auth + RBAC).
This shim only keeps the shared SPA's login flow working on the legacy
server with the demo credentials; none of the Python app's endpoints
perform authorization.
"""
from __future__ import annotations

from fastapi import APIRouter, HTTPException, Response, Cookie

router = APIRouter(prefix="/api/auth", tags=["Auth"])

DEMO_USERNAME = "demo"
DEMO_PASSWORD = "demo"


@router.post("/login")
def login(body: dict, response: Response) -> dict:
    username = str(body.get("username") or "").strip()
    password = str(body.get("password") or "")
    if username != DEMO_USERNAME or password != DEMO_PASSWORD:
        raise HTTPException(401, "Invalid username or password")
    response.set_cookie("fcr_auth", DEMO_USERNAME, httponly=True, samesite="lax")
    return {"username": DEMO_USERNAME, "roles": []}


@router.post("/logout")
def logout(response: Response) -> dict:
    response.delete_cookie("fcr_auth")
    return {"ok": True}


@router.get("/me")
def me(fcr_auth: str | None = Cookie(default=None)) -> dict:
    if fcr_auth != DEMO_USERNAME:
        raise HTTPException(401, "Not authenticated")
    return {"id": "0", "username": DEMO_USERNAME, "roles": []}
