@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Installer-Agent-Atlas.ps1"
if errorlevel 1 pause
