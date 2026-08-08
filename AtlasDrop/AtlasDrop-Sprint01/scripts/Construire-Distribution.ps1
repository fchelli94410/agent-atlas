param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$dist = Join-Path $root "dist"
$appOut = Join-Path $dist "app"
$installerOut = Join-Path $dist "installer"

if (Test-Path $dist) {
    Remove-Item $dist -Recurse -Force
}

New-Item -ItemType Directory -Path $appOut -Force | Out-Null
New-Item -ItemType Directory -Path $installerOut -Force | Out-Null

$app = Join-Path $root "src\AtlasDrop.App\AtlasDrop.App.csproj"
$installer = Join-Path $root "src\AtlasDrop.Installer\AtlasDrop.Installer.csproj"

dotnet publish $app -c $Configuration -r win-x64 --self-contained true -o $appOut
if ($LASTEXITCODE -ne 0) { throw "Publication AtlasDrop.App en echec." }

dotnet publish $installer -c $Configuration -r win-x64 --self-contained true -o $installerOut
if ($LASTEXITCODE -ne 0) { throw "Publication AtlasDrop.Installer en echec." }

Copy-Item $appOut (Join-Path $installerOut "app") -Recurse -Force

$setup = Join-Path $installerOut "AtlasDrop.Installer.exe"

& $setup --self-test (Join-Path $installerOut "app")
if ($LASTEXITCODE -ne 0) { throw "Auto-test installateur en echec." }

Write-Host "[OK] Distribution Atlas Drop construite : $installerOut"
Write-Host "[OK] Double-clic final : AtlasDrop.Installer.exe"
