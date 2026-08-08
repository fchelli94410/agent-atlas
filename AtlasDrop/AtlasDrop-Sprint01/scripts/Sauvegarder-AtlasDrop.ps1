$ErrorActionPreference = "Stop"

$source = Join-Path $env:LOCALAPPDATA "AtlasDrop"
$desktop = [Environment]::GetFolderPath("Desktop")
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$dest = Join-Path $desktop "AtlasDrop-Backup-$stamp"

if (-not (Test-Path -LiteralPath $source)) {
    throw "Aucune donnee Atlas Drop a sauvegarder."
}

Copy-Item -LiteralPath $source -Destination $dest -Recurse -Force

Write-Host "[OK] Sauvegarde creee : $dest"
