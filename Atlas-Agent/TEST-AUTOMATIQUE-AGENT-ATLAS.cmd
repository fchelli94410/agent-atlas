@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-Automatique-Agent-Atlas.ps1"
pause
