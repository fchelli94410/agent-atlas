[CmdletBinding()]
param([switch]$Uninstall)

$ErrorActionPreference = 'Stop'
$root = Join-Path $env:LOCALAPPDATA 'AtlasAgent'
$agentDir = Join-Path $root 'Agent'
$taskName = 'Atlas Agent - Mise a jour Atlas Drop'

if ($Uninstall) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host 'Agent Atlas desactive. Les rapports et sauvegardes sont conserves.' -ForegroundColor Green
    exit 0
}

foreach ($name in @('Agent','Inbox','Work','Backups','Reports','Quarantine','State','Processed')) {
    New-Item -ItemType Directory -Path (Join-Path $root $name) -Force | Out-Null
}

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Agent-Atlas.ps1') -Destination $agentDir -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'agent-config.json') -Destination $agentDir -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Connecter-GitHub-Agent-Atlas.ps1') -Destination $agentDir -Force

$script = Join-Path $agentDir 'Agent-Atlas.ps1'
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$script`" -Watch"
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Days 7) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Description 'Valide, installe et restaure automatiquement Atlas Drop.' -Force | Out-Null

Start-ScheduledTask -TaskName $taskName
Write-Host 'AGENT ATLAS INSTALLE ET ACTIF' -ForegroundColor Green
Write-Host "Dossier surveille : $(Join-Path $root 'Inbox')"
Write-Host 'Etape suivante : lancer CONNECTER-GITHUB-AGENT-ATLAS.cmd une seule fois.' -ForegroundColor Yellow
