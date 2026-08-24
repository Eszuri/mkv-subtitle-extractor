@echo off
setlocal
echo ==========================================================
echo   MKS Subtitle Studio - Complete Binary Release Build
echo ==========================================================

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build_release.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Build completed successfully!
pause
