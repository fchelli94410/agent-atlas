@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0TOUT-FAIRE-ATLAS-DROP.ps1"
if errorlevel 1 pause
