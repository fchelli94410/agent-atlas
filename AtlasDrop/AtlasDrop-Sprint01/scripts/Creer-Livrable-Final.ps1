$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$buildScript = Join-Path $root "scripts\Construire-Distribution.ps1"

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $buildScript
if ($LASTEXITCODE -ne 0) {
    throw "Construction distribution en echec."
}

$installerDir = Join-Path $root "dist\installer"
$zip = Join-Path $root "dist\AtlasDrop-V1-Windows-x64.zip"

if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}

Compress-Archive -Path (Join-Path $installerDir "*") -DestinationPath $zip -Force

Write-Host "[OK] Livrable final : $zip"
