param(
    [Parameter(Mandatory=$true)]
    [string]$BackupPath
)

$ErrorActionPreference = "Stop"

$backup = [System.IO.Path]::GetFullPath($BackupPath)
$target = Join-Path $env:LOCALAPPDATA "AtlasDrop"

if (-not (Test-Path -LiteralPath $backup -PathType Container)) {
    throw "Sauvegarde introuvable : $backup"
}

if (Test-Path -LiteralPath $target) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $safety = "$target-before-restore-$stamp"
    Move-Item -LiteralPath $target -Destination $safety
    Write-Host "[INFO] Copie de securite : $safety"
}

Copy-Item -LiteralPath $backup -Destination $target -Recurse -Force

Write-Host "[OK] Restauration terminee."
