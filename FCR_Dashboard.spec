# -*- mode: python ; coding: utf-8 -*-
"""
FCR Dashboard - PyInstaller build specification
Project : FCR Dashboard v2.0.0
Author  : Farzad Meshkipour / SINOPEC International - YADAVARAN

Build with:
    pyinstaller FCR_Dashboard.spec --clean --noconfirm

Produces an onedir bundle in dist/FCR_Dashboard containing FCR_Dashboard.exe,
the bundled frontend (static/), the backend modules and the version resource
from version_info.txt.

Note: runtime data (fcr_database.db, settings.json, logs/, backups/) lives
next to the executable and is never written into the bundle.
"""
from PyInstaller.utils.hooks import collect_submodules
import os

block_cipher = None

a = Analysis(
    ['launcher.py'],
    pathex=[],
    binaries=[],
    datas=[
        ('static', 'static'),        # frontend bundle: HTML, CSS, JS, Chart.js, icons
        ('main.py', '.'),            # FastAPI backend imported by the launcher
        ('migrate_db.py', '.'),      # schema migration helper
        ('settings.json', '.'),      # default settings template; app recreates defaults if missing
    ],
    hiddenimports=[
        'uvicorn.logging',
        'uvicorn.loops',
        'uvicorn.loops.auto',
        'uvicorn.loops.asyncio',
        'uvicorn.protocols',
        'uvicorn.protocols.http',
        'uvicorn.protocols.http.auto',
        'uvicorn.protocols.websockets',
        'uvicorn.protocols.websockets.auto',
        'uvicorn.lifespan',
        'uvicorn.lifespan.on',
        'h11',
        'httptools',
        'main',
        'fastapi',
        'fastapi.middleware.cors',
        'fastapi.responses',
        'fastapi.staticfiles',
        'starlette',
        'anyio',
    ] + collect_submodules('openpyxl'),
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[
        # Remove heavy/unused dependencies to keep the bundle small
        'pandas', 'numpy', 'scipy', 'matplotlib',
        'tkinter', 'PIL',
        'pytest', 'IPython', 'jupyter',
        'pydoc', 'pythonnet', 'clr',
    ],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name='FCR_Dashboard',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=False,  # windowed application; logs go to logs/app.log
    version='version_info.txt',
    icon='static/icon.ico' if os.path.exists('static/icon.ico') else None,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.zipfiles,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name='FCR_Dashboard',
)
