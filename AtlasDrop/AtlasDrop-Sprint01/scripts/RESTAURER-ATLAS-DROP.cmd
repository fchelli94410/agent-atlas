@echo off
setlocal
set /p BACKUP=Chemin du dossier de sauvegarde Atlas Drop :
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Restaurer-AtlasDrop.ps1" -BackupPath "%BACKUP%"
echo.
pause
