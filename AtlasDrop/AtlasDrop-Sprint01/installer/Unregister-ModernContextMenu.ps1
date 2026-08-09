param(
    [Parameter(Mandatory = $true)]
    [string]$ExternalLocation
)

$ErrorActionPreference = 'Continue'
$packageName = 'AtlasDrop.ContextMenu'
$externalLocationFull = [IO.Path]::GetFullPath($ExternalLocation)
$logPath = Join-Path $externalLocationFull 'modern-context-menu.log'
$thumbprintPath = Join-Path $externalLocationFull 'modern-context-cert.thumbprint'

function Write-AtlasLog([string]$message) {
    try {
        Add-Content -LiteralPath $logPath -Value "$(Get-Date -Format s)  $message" -Encoding UTF8
    }
    catch { }
}

try {
    Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue |
        ForEach-Object {
            Remove-AppxPackage -Package $_.PackageFullName -ErrorAction Continue
            Write-AtlasLog "Identité Atlas Drop retirée : $($_.PackageFullName)"
        }
}
catch {
    Write-AtlasLog "Retrait identité impossible : $($_.Exception.Message)"
}

try {
    if (Test-Path -LiteralPath $thumbprintPath) {
        $thumbprint = (Get-Content -LiteralPath $thumbprintPath -Raw).Trim()
        if ($thumbprint -match '^[0-9A-Fa-f]{40,64}$') {
            foreach ($storePath in @('Cert:\LocalMachine\TrustedPeople', 'Cert:\CurrentUser\TrustedPeople')) {
                Get-ChildItem $storePath -ErrorAction SilentlyContinue |
                    Where-Object { $_.Thumbprint -eq $thumbprint } |
                    ForEach-Object {
                        Remove-Item -LiteralPath $_.PSPath -Force -ErrorAction Continue
                        Write-AtlasLog "Certificat Atlas Drop retiré de $storePath : $thumbprint"
                    }
            }
        }
        Remove-Item -LiteralPath $thumbprintPath -Force -ErrorAction SilentlyContinue
    }
}
catch {
    Write-AtlasLog "Retrait certificat impossible : $($_.Exception.Message)"
}

exit 0
