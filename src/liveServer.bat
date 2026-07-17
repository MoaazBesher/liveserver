@echo off
echo ============================================
echo   Live Server Controller - GUI Available!
echo ============================================
echo.
echo A GUI version is now available.
echo Double-click "liveServer.vbs" to launch it
echo without a terminal window.
echo.
echo Or right-click "liveServer.ps1" and select
echo "Run with PowerShell".
echo.
echo To use the old terminal version, continue below.
echo ============================================
echo.

setlocal enabledelayedexpansion
title Live Server Controller

:LOOP
cls
echo ===========================
echo     LIVE SERVER (LEGACY)
echo ===========================
echo.

set /p projectPath=Enter project path:
set "projectPath=%projectPath:"=%"

if not exist "%projectPath%" (
    echo Invalid path!
    pause
    goto LOOP
)

cd /d "%projectPath%"
echo.
echo Starting server...

start "LIVESERVER_WINDOW" /min cmd /c live-server --port=5500
echo Server started.
echo Press Q then Enter to stop

:WAIT
set /p input=Running... (Q to stop):
if /i "%input%"=="q" (
    echo Stopping server...
    taskkill /FI "WINDOWTITLE eq LIVESERVER_WINDOW*" /F >nul 2>&1
    echo Stopped.
    timeout /t 1 >nul
    goto LOOP
)
goto WAIT
