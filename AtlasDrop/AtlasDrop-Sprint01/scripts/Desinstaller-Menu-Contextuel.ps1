$ErrorActionPreference = "Stop"

$key = "HKCU:\Software\Classes\*\shell\AtlasDrop"

if (Test-Path $key) {
    Remove-Item -Path $key -Recurse -Force
}

Write-Host "Menu contextuel Atlas Drop supprime."
