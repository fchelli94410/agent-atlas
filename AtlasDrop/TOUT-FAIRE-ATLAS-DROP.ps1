param([switch]$SansPause)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Stop-WithMessage([string]$Message) {
    Write-Host ""
    Write-Host "ERREUR : $Message" -ForegroundColor Red
    Write-Host ""
    Read-Host "Appuie sur Entree pour fermer"
    exit 1
}

try {
    $packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $projectRoot = Join-Path $packageRoot "AtlasDrop-Sprint01"
    $solution = Join-Path $projectRoot "AtlasDrop.sln"
    $buildScript = Join-Path $projectRoot "scripts\Construire-Distribution.ps1"

    Clear-Host
    Write-Host "==========================================" -ForegroundColor Cyan
    Write-Host "       ATLAS DROP - INSTALLATION AUTO" -ForegroundColor Cyan
    Write-Host "==========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Ne ferme pas cette fenetre. Tout est automatique." -ForegroundColor Yellow
    Write-Host ""

    if (-not (Test-Path -LiteralPath $solution -PathType Leaf)) {
        throw "Projet Atlas Drop introuvable. Decompresse d'abord completement le ZIP."
    }

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnet) {
        throw ".NET 8 est absent. Relance d'abord l'ancien agent qui l'avait installe."
    }

    $sdks = & dotnet --list-sdks
    if ($LASTEXITCODE -ne 0 -or -not ($sdks -match '^8\.')) {
        throw "Le SDK .NET 8 est absent."
    }

    Write-Host "Fermeture de toute ancienne version..." -ForegroundColor White
    Get-Process -Name "AtlasDrop.App" -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    Write-Host "[1/5] Compilation..." -ForegroundColor White
    & dotnet restore $solution --disable-parallel --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw "La restauration a echoue." }

    & dotnet build $solution -c Release --no-restore --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw "La compilation a echoue." }

    Write-Host "[2/5] Tests..." -ForegroundColor White
    & dotnet test $solution -c Release --no-build --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw "Un test a echoue." }

    Write-Host "[3/5] Preparation de l'application..." -ForegroundColor White
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $buildScript
    if ($LASTEXITCODE -ne 0) { throw "La preparation de l'application a echoue." }

    $installer = Join-Path $projectRoot "dist\installer\AtlasDrop.Installer.exe"
    if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
        throw "L'installateur n'a pas ete cree."
    }

    Write-Host "[4/5] Installation..." -ForegroundColor White
    & $installer
    if ($LASTEXITCODE -ne 0) { throw "L'installation a echoue." }

    $installedExe = Join-Path $env:LOCALAPPDATA "AtlasDrop\app\AtlasDrop.App.exe"
    if (-not (Test-Path -LiteralPath $installedExe -PathType Leaf)) {
        throw "Atlas Drop installe est introuvable."
    }

    $desktop = [Environment]::GetFolderPath("Desktop")
    $shortcutPath = Join-Path $desktop "Atlas Drop.lnk"
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $installedExe
    $shortcut.WorkingDirectory = Split-Path -Parent $installedExe
    $shortcut.Description = "Lancer Atlas Drop"
    $shortcut.Save()

    Write-Host "[5/5] Lancement..." -ForegroundColor White
    Start-Process -FilePath $installedExe
    Start-Sleep -Seconds 2

    $running = Get-Process -Name "AtlasDrop.App" -ErrorAction SilentlyContinue
    if ($null -eq $running) {
        throw "Atlas Drop n'est pas reste lance."
    }

    Write-Host ""
    Write-Host "ATLAS DROP EST INSTALLE ET LANCE." -ForegroundColor Green
    Write-Host "Un raccourci 'Atlas Drop' est maintenant sur ton Bureau." -ForegroundColor Green
    Write-Host "Tu peux tester le clic molette sur un fichier." -ForegroundColor Green
    Write-Host ""
    if (-not $SansPause) {
        Read-Host "Appuie sur Entree pour fermer cette fenetre"
    }
}
catch {
    Stop-WithMessage $_.Exception.Message
}
