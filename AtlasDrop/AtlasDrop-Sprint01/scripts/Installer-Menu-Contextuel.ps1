param(
    [Parameter(Mandatory=$true)]
    [string]$ExePath
)

$ErrorActionPreference = "Stop"

$full = [System.IO.Path]::GetFullPath($ExePath)

if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
    throw "Atlas Drop executable not found: $full"
}

$key = "HKCU:\Software\Classes\*\shell\AtlasDrop"
$command = Join-Path $key "command"

New-Item -Path $key -Force | Out-Null
Set-Item -Path $key -Value "Ranger avec Atlas Drop"
New-ItemProperty -Path $key -Name "Icon" -Value ('"' + $full + '"') -PropertyType String -Force | Out-Null

New-Item -Path $command -Force | Out-Null
Set-Item -Path $command -Value ('"' + $full + '" "%1"')

Write-Host "Menu contextuel Atlas Drop installe pour l'utilisateur courant."
