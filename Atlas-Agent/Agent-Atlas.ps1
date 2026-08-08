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
    do {
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
