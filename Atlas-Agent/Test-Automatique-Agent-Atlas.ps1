$ErrorActionPreference = 'Stop'
$testRoot = Join-Path $env:TEMP ('AtlasAgent-Test-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path (Join-Path $testRoot 'Agent') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'agent-config.json') -Destination (Join-Path $testRoot 'Agent')
    $configPath = Join-Path $testRoot 'Agent\agent-config.json'
    $config = Get-Content $configPath -Raw | ConvertFrom-Json
    if ($config.githubEnabled -ne $false) { throw 'GitHub doit etre desactive avant autorisation.' }
    if ([string]$config.githubRepository -ne 'fchelli94410/agent-atlas') { throw 'Depot GitHub inattendu.' }
    foreach ($requiredFile in @('Connecter-GitHub-Agent-Atlas.ps1','CONNECTER-GITHUB-AGENT-ATLAS.cmd')) {
        if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $requiredFile) -PathType Leaf)) { throw "Fichier absent : $requiredFile" }
    }
    $config.stableSeconds = 0
    $config | ConvertTo-Json -Depth 4 | Set-Content $configPath -Encoding UTF8

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Agent-Atlas.ps1') -Once -RootPath $testRoot
    if ($LASTEXITCODE -ne 0) { throw 'Demarrage agent echoue.' }
    foreach ($folder in @('Inbox','Work','Backups','Reports','Quarantine','State','Processed')) {
        if (-not (Test-Path (Join-Path $testRoot $folder))) { throw "Dossier absent : $folder" }
    }

    Set-Content -LiteralPath (Join-Path $testRoot 'Inbox\bad.zip') -Value 'altered' -Encoding ASCII
    $bad = [ordered]@{schemaVersion=1;product='AtlasDrop';version='1.0.0';packageFile='AtlasDrop-1.0.0.zip';sha256=('0'*64);expectedExecutable='AtlasDrop.App.exe';steps=@('self-test');timeoutMinutes=2}
    Move-Item (Join-Path $testRoot 'Inbox\bad.zip') (Join-Path $testRoot 'Inbox\AtlasDrop-1.0.0.zip')
    $bad | ConvertTo-Json | Set-Content (Join-Path $testRoot 'Inbox\AtlasDrop-1.0.0.manifest.json') -Encoding UTF8
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Agent-Atlas.ps1') -Once -RootPath $testRoot
    if (-not (Get-ChildItem (Join-Path $testRoot 'Reports') -Filter '*.result.json')) { throw 'Rapport non cree.' }
    if (-not (Get-ChildItem (Join-Path $testRoot 'Quarantine') -Filter '*.manifest.json')) { throw 'Quarantaine non appliquee.' }
    Write-Host 'TEST AUTOMATIQUE REUSSI' -ForegroundColor Green
}
catch {
    Write-Host "TEST AUTOMATIQUE ECHEC : $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}
