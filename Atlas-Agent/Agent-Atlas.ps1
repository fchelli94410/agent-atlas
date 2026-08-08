[CmdletBinding()]
param(
    [switch]$Watch,
    [switch]$Once,
    [string]$RootPath = (Join-Path $env:LOCALAPPDATA 'AtlasAgent')
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Expand-EnvironmentPath([string]$Value) {
    [Environment]::ExpandEnvironmentVariables($Value)
}

function New-AgentFolders([string]$Root) {
    $map = @{}
    foreach ($name in @('Agent','Inbox','Work','Backups','Reports','Quarantine','State','Processed')) {
        $path = Join-Path $Root $name
        New-Item -ItemType Directory -Path $path -Force | Out-Null
        $map[$name] = $path
    }
    return $map
}

function Write-AgentLog([string]$Path, [string]$Level, [string]$Message) {
    $line = '{0} [{1}] {2}' -f ([DateTime]::UtcNow.ToString('o')), $Level, $Message
    Add-Content -LiteralPath $Path -Value $line -Encoding UTF8
}

function Write-ResultReport($Context, [string]$Status, [string]$Message, [bool]$RolledBack) {
    $ended = [DateTime]::UtcNow
    $report = [ordered]@{
        operationId = $Context.OperationId
        product = 'AtlasDrop'
        version = $Context.Version
        status = $Status
        message = $Message
        rolledBack = $RolledBack
        startedUtc = $Context.StartedUtc.ToString('o')
        endedUtc = $ended.ToString('o')
        durationSeconds = [Math]::Round(($ended - $Context.StartedUtc).TotalSeconds, 2)
        machine = $env:COMPUTERNAME
        logFile = [IO.Path]::GetFileName($Context.LogPath)
    }
    $reportPath = Join-Path $Context.Folders.Reports ("{0}-{1}.result.json" -f $Context.Timestamp, $Context.OperationId)
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding UTF8
}

function Move-PackageFiles($Context, [string]$Destination) {
    foreach ($path in @($Context.ManifestPath, $Context.PackagePath)) {
        if (-not [string]::IsNullOrWhiteSpace([string]$path) -and (Test-Path -LiteralPath $path)) {
            Move-Item -LiteralPath $path -Destination $Destination -Force
        }
    }
}

function Find-GitHubCli {
    $command = Get-Command gh.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) { return $command.Source }
    $candidates = @(
        (Join-Path $env:ProgramFiles 'GitHub CLI\gh.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\GitHub CLI\gh.exe')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    $wingetRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
    if (Test-Path -LiteralPath $wingetRoot) {
        $found = Get-ChildItem -LiteralPath $wingetRoot -Filter 'gh.exe' -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $found) { return $found.FullName }
    }
    return $null
}

function Get-RemoteState([string]$StatePath) {
    try {
        if (Test-Path -LiteralPath $StatePath -PathType Leaf) {
            return Get-Content -LiteralPath $StatePath -Raw -Encoding UTF8 | ConvertFrom-Json
        }
    }
    catch { }
    return [pscustomobject]@{ lastQueuedCommit = '' }
}

function Save-RemoteState([string]$StatePath, [string]$Commit) {
    [ordered]@{ lastQueuedCommit=$Commit; queuedUtc=[DateTime]::UtcNow.ToString('o') } |
        ConvertTo-Json | Set-Content -LiteralPath $StatePath -Encoding UTF8
}

function Queue-GitHubUpdate($Folders, $Config, [string]$LogPath) {
    if (-not [bool]$Config.githubEnabled) { return }
    $repository = [string]$Config.githubRepository
    $branch = [string]$Config.githubBranch
    if ($repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Depot GitHub invalide.' }
    if ($branch -notmatch '^[A-Za-z0-9._/-]+$') { throw 'Branche GitHub invalide.' }

    $gh = Find-GitHubCli
    if ([string]::IsNullOrWhiteSpace($gh)) { throw 'GitHub CLI absent. Lance CONNECTER-GITHUB-AGENT-ATLAS.cmd.' }
    & $gh auth status --hostname github.com 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Connexion GitHub absente ou expiree.' }

    $commit = (& $gh api "repos/$repository/commits/$branch" --jq '.sha' 2>$null | Select-Object -First 1).Trim()
    if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[a-fA-F0-9]{40}$') { throw 'Commit GitHub distant introuvable.' }
    $statePath = Join-Path $Folders.State 'github-state.json'
    $state = Get-RemoteState $statePath
    if ([string]$state.lastQueuedCommit -eq $commit) { return }

    $downloadRoot = Join-Path $Folders.Work ('github-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $downloadRoot -Force | Out-Null
    try {
        $archivePath = Join-Path $downloadRoot 'repository.zip'
        $token = (& $gh auth token --hostname github.com 2>$null | Select-Object -First 1).Trim()
        if ([string]::IsNullOrWhiteSpace($token)) { throw 'Jeton GitHub indisponible.' }
        $headers = @{ Authorization="Bearer $token"; Accept='application/vnd.github+json'; 'User-Agent'='Atlas-Agent' }
        Invoke-WebRequest -Uri "https://api.github.com/repos/$repository/zipball/$branch" -Headers $headers -OutFile $archivePath -UseBasicParsing
        $extractPath = Join-Path $downloadRoot 'repository'
        Assert-SafeZip $archivePath $extractPath
        New-Item -ItemType Directory -Path $extractPath -Force | Out-Null
        Expand-Archive -LiteralPath $archivePath -DestinationPath $extractPath -Force
        $appRoot = Get-ChildItem -LiteralPath $extractPath -Directory | ForEach-Object { Join-Path $_.FullName 'AtlasDrop' } | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -First 1
        if ([string]::IsNullOrWhiteSpace($appRoot)) { throw 'Dossier AtlasDrop absent du depot GitHub.' }
        $readme = Join-Path $appRoot 'README.txt'
        $baseVersion = '1.0.0'
        if (Test-Path -LiteralPath $readme -PathType Leaf) {
            $match = [regex]::Match((Get-Content -LiteralPath $readme -Raw -Encoding UTF8), 'v(\d+\.\d+\.\d+)')
            if ($match.Success) { $baseVersion = $match.Groups[1].Value }
        }
        $version = "$baseVersion-$($commit.Substring(0,7).ToLowerInvariant())"
        $packageName = "AtlasDrop-$version.zip"
        $packagePath = Join-Path $Folders.Inbox $packageName
        if (Test-Path -LiteralPath $packagePath) { Remove-Item -LiteralPath $packagePath -Force }
        Compress-Archive -Path (Join-Path $appRoot '*') -DestinationPath $packagePath -CompressionLevel Optimal
        $hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
        $manifest = [ordered]@{
            schemaVersion=1; product='AtlasDrop'; version=$version; packageFile=$packageName
            sha256=$hash; expectedExecutable='AtlasDrop.App.exe'; steps=@('restore','build','test'); timeoutMinutes=30
            sourceRepository=$repository; sourceBranch=$branch; sourceCommit=$commit; createdUtc=[DateTime]::UtcNow.ToString('o')
        }
        $manifestPath = Join-Path $Folders.Inbox "AtlasDrop-$version.manifest.json"
        $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
        Save-RemoteState $statePath $commit
        Write-AgentLog $LogPath 'INFO' "Mise a jour GitHub mise en attente : $commit"
    }
    finally {
        Remove-Item -LiteralPath $downloadRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Assert-SafeManifest($Manifest, [string]$ManifestPath, $Config) {
    $required = @('schemaVersion','product','version','packageFile','sha256','expectedExecutable','steps','timeoutMinutes')
    foreach ($field in $required) {
        if ($null -eq $Manifest.$field -or [string]::IsNullOrWhiteSpace([string]$Manifest.$field)) {
            throw "Champ obligatoire absent : $field"
        }
    }
    if ([int]$Manifest.schemaVersion -ne 1) { throw 'Version de manifeste non prise en charge.' }
    if ([string]$Manifest.product -ne 'AtlasDrop') { throw 'Produit non autorise.' }
    if ([string]$Manifest.version -notmatch '^\d+\.\d+\.\d+([-.][A-Za-z0-9.]+)?$') { throw 'Version invalide.' }
    if ([IO.Path]::GetFileName([string]$Manifest.packageFile) -ne [string]$Manifest.packageFile) { throw 'Nom de paquet dangereux.' }
    if ([string]$Manifest.packageFile -notmatch '^AtlasDrop-[A-Za-z0-9._-]+\.zip$') { throw 'Nom de paquet refuse.' }
    if ([string]$Manifest.sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Empreinte SHA-256 invalide.' }
    if ([int]$Manifest.timeoutMinutes -lt 1 -or [int]$Manifest.timeoutMinutes -gt 60) { throw 'Delai maximal invalide.' }
    $allowed = @($Config.allowedSteps)
    foreach ($step in @($Manifest.steps)) {
        if ($allowed -notcontains [string]$step) { throw "Etape non autorisee : $step" }
    }
    if ([IO.Path]::GetFileName([string]$Manifest.expectedExecutable) -ne [string]$Manifest.expectedExecutable) {
        throw 'Nom executable dangereux.'
    }
}

function Assert-SafeZip([string]$ZipPath, [string]$Destination) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $root = [IO.Path]::GetFullPath($Destination + [IO.Path]::DirectorySeparatorChar)
    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        foreach ($entry in $archive.Entries) {
            $candidate = [IO.Path]::GetFullPath((Join-Path $Destination $entry.FullName))
            if (-not $candidate.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Archive dangereuse : $($entry.FullName)"
            }
            if ($entry.FullName -match '(^|[\\/])\.\.([\\/]|$)') { throw 'Archive avec traversee de chemin.' }
        }
    }
    finally { $archive.Dispose() }
}

function Invoke-ProcessChecked([string]$FilePath, [string[]]$Arguments, [string]$WorkingDirectory, [int]$TimeoutMinutes, [string]$LogPath) {
    $stdout = [IO.Path]::GetTempFileName()
    $stderr = [IO.Path]::GetTempFileName()
    try {
        $argLine = ($Arguments | ForEach-Object { if ($_ -match '[\s"]') { '"' + ($_ -replace '"','\"') + '"' } else { $_ } }) -join ' '
        Write-AgentLog $LogPath 'INFO' "Execution : $FilePath $argLine"
        $p = Start-Process -FilePath $FilePath -ArgumentList $argLine -WorkingDirectory $WorkingDirectory -NoNewWindow -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        if (-not $p.WaitForExit($TimeoutMinutes * 60 * 1000)) {
            Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
            throw "Delai depasse : $FilePath"
        }
        Get-Content -LiteralPath $stdout -ErrorAction SilentlyContinue | Add-Content -LiteralPath $LogPath -Encoding UTF8
        Get-Content -LiteralPath $stderr -ErrorAction SilentlyContinue | Add-Content -LiteralPath $LogPath -Encoding UTF8
        if ($p.ExitCode -ne 0) { throw "Commande echouee avec le code $($p.ExitCode)." }
    }
    finally {
        Remove-Item -LiteralPath $stdout,$stderr -Force -ErrorAction SilentlyContinue
    }
}

function Find-Solution([string]$Root) {
    @(Get-ChildItem -LiteralPath $Root -Filter '*.sln' -File -Recurse -ErrorAction SilentlyContinue) | Select-Object -First 1
}

function Find-Payload([string]$Root, [string]$ExpectedExecutable) {
    $app = Join-Path $Root 'app'
    if (Test-Path -LiteralPath (Join-Path $app $ExpectedExecutable) -PathType Leaf) { return $app }
    $executables = @(Get-ChildItem -LiteralPath $Root -Filter $ExpectedExecutable -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '[\\/]bin[\\/]Release[\\/]' } |
        Sort-Object FullName)
    if ($executables.Count -eq 0) {
        $executables = @(Get-ChildItem -LiteralPath $Root -Filter $ExpectedExecutable -File -Recurse -ErrorAction SilentlyContinue | Sort-Object FullName)
    }
    if ($executables.Count -eq 0) { throw "Executable attendu introuvable : $ExpectedExecutable" }
    return $executables[0].Directory.FullName
}

function Invoke-ValidationSteps($Context, $Manifest, [string]$ExtractPath) {
    $solution = Find-Solution $ExtractPath
    foreach ($step in @($Manifest.steps)) {
        switch ([string]$step) {
            'restore' {
                if ($null -eq $solution) { throw 'Solution .sln absente pour restore.' }
                Invoke-ProcessChecked 'dotnet' @('restore',$solution.FullName,'--disable-parallel','--verbosity','minimal') $solution.Directory.FullName $Manifest.timeoutMinutes $Context.LogPath
            }
            'build' {
                if ($null -eq $solution) { throw 'Solution .sln absente pour build.' }
                Invoke-ProcessChecked 'dotnet' @('build',$solution.FullName,'-c','Release','--no-restore','--verbosity','minimal') $solution.Directory.FullName $Manifest.timeoutMinutes $Context.LogPath
            }
            'test' {
                if ($null -eq $solution) { throw 'Solution .sln absente pour test.' }
                Invoke-ProcessChecked 'dotnet' @('test',$solution.FullName,'-c','Release','--no-build','--verbosity','minimal') $solution.Directory.FullName $Manifest.timeoutMinutes $Context.LogPath
            }
            'self-test' { }
            default { throw "Etape refusee : $step" }
        }
    }
}

function Install-Atomically($Context, [string]$Payload, [string]$InstallPath, [string]$ExpectedExecutable, [bool]$RunSelfTest) {
    $parent = Split-Path -Parent $InstallPath
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    $stage = Join-Path $parent ('app.new.' + $Context.OperationId)
    $old = Join-Path $parent ('app.old.' + $Context.OperationId)
    Copy-Item -LiteralPath $Payload -Destination $stage -Recurse -Force
    if (-not (Test-Path -LiteralPath (Join-Path $stage $ExpectedExecutable) -PathType Leaf)) { throw 'Payload incomplet.' }

    Get-Process -Name 'AtlasDrop.App' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $InstallPath) { Move-Item -LiteralPath $InstallPath -Destination $old }
    try {
        Move-Item -LiteralPath $stage -Destination $InstallPath
        $exe = Join-Path $InstallPath $ExpectedExecutable
        if ($RunSelfTest) { Invoke-ProcessChecked $exe @('--self-test') $InstallPath 2 $Context.LogPath }
        Start-Process -FilePath $exe -WorkingDirectory $InstallPath
        Start-Sleep -Seconds 3
        if ($null -eq (Get-Process -Name 'AtlasDrop.App' -ErrorAction SilentlyContinue)) { throw 'Atlas Drop ne reste pas lance.' }
        if (Test-Path -LiteralPath $old) {
            $backup = Join-Path $Context.Folders.Backups ("AtlasDrop-{0}-{1}" -f $Context.Version,$Context.Timestamp)
            Move-Item -LiteralPath $old -Destination $backup
        }
        return $false
    }
    catch {
        Get-Process -Name 'AtlasDrop.App' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $InstallPath) { Remove-Item -LiteralPath $InstallPath -Recurse -Force }
        if (Test-Path -LiteralPath $old) {
            Move-Item -LiteralPath $old -Destination $InstallPath
            $previousExe = Join-Path $InstallPath $ExpectedExecutable
            if (Test-Path -LiteralPath $previousExe) { Start-Process -FilePath $previousExe -WorkingDirectory $InstallPath }
        }
        throw
    }
    finally {
        Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Limit-Backups([string]$Folder, [int]$Maximum) {
    @(Get-ChildItem -LiteralPath $Folder -Directory | Sort-Object LastWriteTimeUtc -Descending | Select-Object -Skip $Maximum) |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

function Process-Manifest([IO.FileInfo]$ManifestFile, $Folders, $Config) {
    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $operationId = [Guid]::NewGuid().ToString('N').Substring(0,12)
    $logPath = Join-Path $Folders.Reports "$timestamp-$operationId.log"
    $context = [pscustomobject]@{ OperationId=$operationId; Timestamp=$timestamp; StartedUtc=[DateTime]::UtcNow; Version='unknown'; LogPath=$logPath; Folders=$Folders; ManifestPath=$ManifestFile.FullName; PackagePath=$null }
    $rolledBack = $false
    try {
        Write-AgentLog $logPath 'INFO' "Manifeste detecte : $($ManifestFile.Name)"
        if (((Get-Date) - $ManifestFile.LastWriteTime).TotalSeconds -lt [int]$Config.stableSeconds) { return }
        $manifest = Get-Content -LiteralPath $ManifestFile.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        Assert-SafeManifest $manifest $ManifestFile.FullName $Config
        $context.Version = [string]$manifest.version
        $context.PackagePath = Join-Path $Folders.Inbox ([string]$manifest.packageFile)
        if (-not (Test-Path -LiteralPath $context.PackagePath -PathType Leaf)) { return }
        $packageInfo = Get-Item -LiteralPath $context.PackagePath
        if ($packageInfo.Length -gt [long]$Config.maxPackageBytes) { throw 'Paquet trop volumineux.' }
        if (((Get-Date) - $packageInfo.LastWriteTime).TotalSeconds -lt [int]$Config.stableSeconds) { return }
        $actualHash = (Get-FileHash -LiteralPath $context.PackagePath -Algorithm SHA256).Hash
        if (-not $actualHash.Equals([string]$manifest.sha256, [StringComparison]::OrdinalIgnoreCase)) { throw 'Empreinte SHA-256 incorrecte.' }

        $work = Join-Path $Folders.Work $operationId
        New-Item -ItemType Directory -Path $work -Force | Out-Null
        Assert-SafeZip $context.PackagePath $work
        Expand-Archive -LiteralPath $context.PackagePath -DestinationPath $work -Force
        Invoke-ValidationSteps $context $manifest $work
        $payload = Find-Payload $work ([string]$manifest.expectedExecutable)
        $installPath = Expand-EnvironmentPath ([string]$Config.atlasDropInstallPath)
        $runSelfTest = @($manifest.steps) -contains 'self-test'
        Install-Atomically $context $payload $installPath ([string]$manifest.expectedExecutable) $runSelfTest | Out-Null
        Limit-Backups $Folders.Backups ([int]$Config.maxBackups)
        Move-PackageFiles $context $Folders.Processed
        Write-AgentLog $logPath 'SUCCESS' 'Installation validee.'
        Write-ResultReport $context 'SUCCESS' 'Atlas Drop installe et valide automatiquement.' $false
    }
    catch {
        $message = $_.Exception.Message
        Write-AgentLog $logPath 'ERROR' $message
        Move-PackageFiles $context $Folders.Quarantine
        Write-ResultReport $context 'FAILED' $message $rolledBack
    }
    finally {
        Remove-Item -LiteralPath (Join-Path $Folders.Work $operationId) -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$folders = New-AgentFolders $RootPath
$configPath = Join-Path $folders.Agent 'agent-config.json'
if (-not (Test-Path -LiteralPath $configPath)) { $configPath = Join-Path $PSScriptRoot 'agent-config.json' }
$config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
$mutex = New-Object Threading.Mutex($false, 'Local\AtlasAgent-v1')
if (-not $mutex.WaitOne(0)) { exit 0 }

try {
    $nextGitHubCheck = [DateTime]::MinValue
    do {
        if ([DateTime]::UtcNow -ge $nextGitHubCheck) {
            $syncLog = Join-Path $folders.Reports 'github-sync.log'
            try { Queue-GitHubUpdate $folders $config $syncLog }
            catch { Write-AgentLog $syncLog 'ERROR' $_.Exception.Message }
            $minutes = if ($null -ne $config.githubPollMinutes) { [Math]::Max(1,[int]$config.githubPollMinutes) } else { 5 }
            $nextGitHubCheck = [DateTime]::UtcNow.AddMinutes($minutes)
        }
        @(Get-ChildItem -LiteralPath $folders.Inbox -Filter '*.manifest.json' -File -ErrorAction SilentlyContinue | Sort-Object CreationTimeUtc) |
            ForEach-Object { Process-Manifest $_ $folders $config }
        if (-not $Watch) { break }
        Start-Sleep -Seconds ([Math]::Max(5,[int]$config.pollSeconds))
    } while ($true)
}
finally {
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
