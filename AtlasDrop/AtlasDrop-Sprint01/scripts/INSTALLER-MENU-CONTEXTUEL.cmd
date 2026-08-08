@echo off
setlocal

set "HERE=%~dp0"
set "EXE=%HERE%..\src\AtlasDrop.App\bin\Release\net8.0-windows\AtlasDrop.App.exe"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%HERE%Installer-Menu-Contextuel.ps1" -ExePath "%EXE%"

echo.
pause
