[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceFolder,
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+([-.][A-Za-z0-9.]+)?$')][string]$Version,
    [string]$OutputFolder = (Join-Path $PSScriptRoot 'PackageOutput'),
    [string[]]$Steps = @('restore','build','test','self-test')
)

$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $SourceFolder).Path
New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null
$zipName = "AtlasDrop-$Version.zip"
$zipPath = Join-Path $OutputFolder $zipName
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -Path (Join-Path $source '*') -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest = [ordered]@{
    schemaVersion = 1
    product = 'AtlasDrop'
    version = $Version
    packageFile = $zipName
    sha256 = $hash
    expectedExecutable = 'AtlasDrop.App.exe'
    steps = $Steps
    timeoutMinutes = 20
    createdUtc = [DateTime]::UtcNow.ToString('o')
}
$manifestPath = Join-Path $OutputFolder "AtlasDrop-$Version.manifest.json"
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Write-Host "Paquet cree : $zipPath" -ForegroundColor Green
Write-Host "Manifeste : $manifestPath" -ForegroundColor Green
