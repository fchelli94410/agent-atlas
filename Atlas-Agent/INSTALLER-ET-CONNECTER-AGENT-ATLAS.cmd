@echo off
setlocal
title Agent Atlas - Installation automatique
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Installer-Agent-Atlas.ps1"
if errorlevel 1 goto :error
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Connecter-GitHub-Agent-Atlas.ps1"
if errorlevel 1 goto :error
echo.
echo AGENT ATLAS EST INSTALLE ET CONNECTE.
timeout /t 5 /nobreak >nul
exit /b 0

:error
echo.
echo INSTALLATION NON TERMINEE.
pause
exit /b 1
