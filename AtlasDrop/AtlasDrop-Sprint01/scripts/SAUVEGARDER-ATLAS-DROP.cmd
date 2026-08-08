@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Sauvegarder-AtlasDrop.ps1"
echo.
pause
