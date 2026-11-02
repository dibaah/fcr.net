@echo off
title FCR Dashboard — Dev Server
color 1F & cls
echo ============================================================
echo   FCR Dashboard — Development Server
echo   Author: Farzad Meshkipour / SINOPEC International
echo ============================================================
echo.

:: Check Python
python --version >nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Python not found! Please install Python 3.11+
    pause
    exit /b 1
)
echo [OK] Python found:
python --version
echo.

:: Check requirements.txt
IF NOT EXIST "requirements.txt" (
    echo [ERROR] requirements.txt not found!
    pause
    exit /b 1
)

:: Create venv if missing
IF NOT EXIST "venv\" (
    echo [SETUP] Creating virtual environment...
    python -m venv venv
    IF %ERRORLEVEL% NEQ 0 (
        echo [ERROR] Failed to create venv
        pause
        exit /b 1
    )
)

:: Activate venv
call venv\Scripts\activate.bat
echo [OK] Virtual environment active
echo.

:: Install dependencies
echo [SETUP] Checking dependencies...
pip install -r requirements.txt --disable-pip-version-check
IF %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to install dependencies
    pause
    exit /b 1
)
echo [OK] All packages ready
echo.

:: Check static folder
IF NOT EXIST "static\" (
    echo [WARNING] static\ folder not found. Creating...
    mkdir static
    echo [WARNING] Please add your HTML/CSS/JS files to static\ folder
    echo.
)

:: Check database
IF NOT EXIST "fcr_database.db" (
    echo [INFO] Database not found. It will be created on first run.
    echo.
)

echo ============================================================
echo   Starting development server...
echo   URL: http://localhost:8000
echo   API Docs: http://localhost:8000/api/docs
echo   Press Ctrl+C to stop
echo ============================================================
echo.

:: Open browser after 2 seconds
start "" /min cmd /c "timeout /t 2 /nobreak >nul && start http://localhost:8000"

:: Run server with hot reload
uvicorn main:app --host 127.0.0.1 --port 8000 --reload

pause