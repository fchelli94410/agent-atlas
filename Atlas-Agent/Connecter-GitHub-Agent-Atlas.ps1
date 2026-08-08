[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repository = 'fchelli94410/agent-atlas'
$taskName = 'Atlas Agent - Mise a jour Atlas Drop'
$agentRoot = Join-Path $env:LOCALAPPDATA 'AtlasAgent\Agent'
$configPath = Join-Path $agentRoot 'agent-config.json'

function Find-GitHubCli {
    $command = Get-Command gh.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) { return $command.Source }
    foreach ($candidate in @((Join-Path $env:ProgramFiles 'GitHub CLI\gh.exe'),(Join-Path $env:LOCALAPPDATA 'Programs\GitHub CLI\gh.exe'))) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    $wingetRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
    if (Test-Path -LiteralPath $wingetRoot) {
        $found = Get-ChildItem -LiteralPath $wingetRoot -Filter 'gh.exe' -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $found) { return $found.FullName }
    }
    return $null
}

try {
    if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) { throw 'Installe d abord Agent Atlas.' }
    $gh = Find-GitHubCli
    if ([string]::IsNullOrWhiteSpace($gh)) {
        $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
        if ($null -eq $winget) { throw 'GitHub CLI et winget sont absents.' }
        Write-Host 'Installation securisee de GitHub CLI...' -ForegroundColor Cyan
        & $winget.Source install --id GitHub.cli --exact --source winget --accept-package-agreements --accept-source-agreements --silent
        if ($LASTEXITCODE -ne 0) { throw 'Installation de GitHub CLI impossible.' }
        $gh = Find-GitHubCli
        if ([string]::IsNullOrWhiteSpace($gh)) { throw 'GitHub CLI installe mais introuvable. Redemarre Windows puis relance ce fichier.' }
    }

    & $gh auth status --hostname github.com 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Ton navigateur va s ouvrir pour une autorisation GitHub unique.' -ForegroundColor Yellow
        & $gh auth login --hostname github.com --git-protocol https --web
        if ($LASTEXITCODE -ne 0) { throw 'Connexion GitHub annulee ou echouee.' }
    }
    $resolved = (& $gh api "repos/$repository" --jq '.full_name' 2>$null | Select-Object -First 1).Trim()
    if ($resolved -ne $repository) { throw 'Le depot prive agent-atlas est inaccessible.' }

    $config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $config.githubEnabled = $true
    $config.githubRepository = $repository
    $config.githubBranch = 'main'
    $config | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $configPath -Encoding UTF8
    Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    Start-ScheduledTask -TaskName $taskName
    Write-Host ''
    Write-Host 'CONNEXION REUSSIE - AGENT ATLAS EST AUTOMATIQUE' -ForegroundColor Green
    Write-Host 'Tu peux fermer cette fenetre.' -ForegroundColor Green
}
catch {
    Write-Host ''
    Write-Host "ERREUR : $($_.Exception.Message)" -ForegroundColor Red
    Read-Host 'Appuie sur Entree pour fermer'
    exit 1
}
