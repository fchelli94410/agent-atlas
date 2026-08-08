@echo off
setlocal

set "HERE=%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%HERE%Desinstaller-Menu-Contextuel.ps1"

echo.
pause
