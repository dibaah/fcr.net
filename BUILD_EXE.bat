@echo off
setlocal EnableExtensions EnableDelayedExpansion
title FCR Dashboard v2.0.0 - Build EXE
color 1F
cls

echo ============================================================
echo   FCR Dashboard v2.0.0 - Windows EXE Build
echo   Author : Farzad Meshkipour / SINOPEC International
echo ============================================================
echo.

set "SPEC_FILE=FCR_Dashboard.spec"
set "DIST_DIR=dist\FCR_Dashboard"

:: ------------------------------------------------------------
:: 1) Python
:: ------------------------------------------------------------
python --version >nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Python was not found in PATH.
    echo         Install Python 3.11 or newer from https://www.python.org/downloads/
    echo         and enable "Add python.exe to PATH" during setup.
    pause
    exit /b 1
)
for /f "delims=" %%V in ('python --version 2^>^&1') do echo [OK] %%V
echo.

:: ------------------------------------------------------------
:: 2) Spec file (the real filename is FCR_Dashboard.spec)
:: ------------------------------------------------------------
IF NOT EXIST "%SPEC_FILE%" (
    echo [ERROR] %SPEC_FILE% not found.
    echo         Run this script from the repository root.
    pause
    exit /b 1
)
echo [OK] Found %SPEC_FILE%
echo.

:: ------------------------------------------------------------
:: 3) Frontend bundle
:: ------------------------------------------------------------
IF NOT EXIST "static\" (
    echo [ERROR] static\ folder not found.
    echo         The frontend bundle is required to build the application.
    pause
    exit /b 1
)
echo [OK] Found static\

IF NOT EXIST "static\chart.min.js" (
    echo [WARNING] static\chart.min.js not found.
    echo           Charts will not render in the built application.
    echo           Restore the bundled Chart.js file before distributing.
    echo.
    choice /C YN /M "Continue anyway"
    IF ERRORLEVEL 2 (
        echo [ABORT] Build cancelled by user.
        pause
        exit /b 1
    )
    echo.
)
echo.

:: ------------------------------------------------------------
:: 4) Virtual environment
:: ------------------------------------------------------------
IF NOT EXIST "venv\Scripts\python.exe" (
    echo [SETUP] Creating virtual environment in venv\ ...
    python -m venv venv
    IF !ERRORLEVEL! NEQ 0 (
        echo [ERROR] Could not create the virtual environment.
        pause
        exit /b 1
    )
)
call "venv\Scripts\activate.bat"
IF %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Could not activate the virtual environment.
    pause
    exit /b 1
)
echo [OK] Virtual environment active
echo.

:: ------------------------------------------------------------
:: 5) Dependencies
:: ------------------------------------------------------------
echo [SETUP] Installing dependencies from requirements.txt ...
python -m pip install --upgrade pip --disable-pip-version-check -q >nul 2>&1
python -m pip install -r requirements.txt --disable-pip-version-check
IF %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Dependency installation failed.
    echo         On an offline machine, stage the wheels first:
    echo             pip download -r requirements.txt -d wheels
    echo             pip install --no-index --find-links wheels -r requirements.txt
    pause
    exit /b 1
)
echo [OK] Dependencies installed
echo.

:: ------------------------------------------------------------
:: 6) Clean previous build
:: ------------------------------------------------------------
IF EXIST "build\" rmdir /s /q "build"
IF EXIST "dist\" rmdir /s /q "dist"
echo [OK] Cleaned build\ and dist\
echo.

:: ------------------------------------------------------------
:: 7) PyInstaller
:: ------------------------------------------------------------
echo [BUILD] Running PyInstaller. This can take 1 to 3 minutes ...
python -m PyInstaller "%SPEC_FILE%" --clean --noconfirm
IF %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] PyInstaller build failed. Review the output above.
    pause
    exit /b 1
)
IF NOT EXIST "%DIST_DIR%\FCR_Dashboard.exe" (
    echo [ERROR] Build completed but %DIST_DIR%\FCR_Dashboard.exe is missing.
    pause
    exit /b 1
)
echo [OK] Built %DIST_DIR%\FCR_Dashboard.exe
echo.

:: ------------------------------------------------------------
:: 8) Runtime files
:: ------------------------------------------------------------
IF EXIST "fcr_database.db" (
    copy /Y "fcr_database.db" "%DIST_DIR%\fcr_database.db" >nul
    echo [OK] Copied fcr_database.db
) else (
    echo [INFO] fcr_database.db not present. It will be created on first run.
)
IF EXIST "migrate_db.py" (
    copy /Y "migrate_db.py" "%DIST_DIR%\migrate_db.py" >nul
    echo [OK] Copied migrate_db.py
)
echo.

:: ------------------------------------------------------------
:: 9) Deployment note
:: ------------------------------------------------------------
echo [INFO] Writing deployment note ...
(
    echo FCR Dashboard v2.0.0
    echo ====================
    echo.
    echo SINOPEC International - YADAVARAN
    echo Author: Farzad Meshkipour
    echo.
    echo DEPLOYMENT
    echo ----------
    echo 1. Copy the entire FCR_Dashboard folder to the target Windows machine.
    echo 2. Make sure Microsoft Edge is installed. It ships with Windows 10 and 11.
    echo    Download: https://www.microsoft.com/edge
    echo 3. Double-click FCR_Dashboard.exe. The application starts its local server
    echo    and opens in Microsoft Edge app mode.
    echo.
    echo FILES
    echo -----
    echo FCR_Dashboard.exe   Application launcher
    echo fcr_database.db     SQLite database - copied from the build or created on first run
    echo settings.json       Configuration - defaults are created when missing
    echo backups\            Database backups
    echo logs\               Application log file, see logs\app.log
    echo.
    echo NOTES
    echo -----
    echo - The server listens on 127.0.0.1 only and picks the next free port from 8000.
    echo - The application runs fully offline. No internet access is required.
    echo - Keep the whole folder together. Do not move the .exe on its own.
    echo - The database and settings.json are written next to the .exe.
    echo.
    echo Build date: %date%
) > "%DIST_DIR%\README.txt"
echo [OK] Wrote %DIST_DIR%\README.txt
echo.

:: ------------------------------------------------------------
:: 10) Summary
:: ------------------------------------------------------------
echo ============================================================
echo   BUILD SUCCESSFUL - FCR Dashboard v2.0.0
echo ============================================================
echo.
echo   Executable : "%CD%\%DIST_DIR%\FCR_Dashboard.exe"
echo   Output     : "%CD%\dist\FCR_Dashboard\"
echo.
echo   Next steps:
echo   - Distribute the whole dist\FCR_Dashboard\ folder, not just the .exe.
echo   - Microsoft Edge is required on the target machine.
echo   - Database and settings are created beside the .exe on first run.
echo.
echo ============================================================
echo.

explorer "%DIST_DIR%"
pause
