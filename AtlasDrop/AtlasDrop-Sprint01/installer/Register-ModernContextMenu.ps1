param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [string]$ExternalLocation,

    [Parameter(Mandatory = $true)]
    [string]$CertificatePath
)

$ErrorActionPreference = 'Stop'
$packageName = 'AtlasDrop.ContextMenu'
$logPath = Join-Path $ExternalLocation 'modern-context-menu.log'
$thumbprintPath = Join-Path $ExternalLocation 'modern-context-cert.thumbprint'

function Write-AtlasLog([string]$message) {
    try {
        Add-Content -LiteralPath $logPath -Value "$(Get-Date -Format s)  $message" -Encoding UTF8
    }
    catch { }
}

try {
    $osBuild = [Environment]::OSVersion.Version.Build
    if ($osBuild -lt 19041) {
        Write-AtlasLog "Menu moderne ignoré : Windows build $osBuild inférieur à 19041."
        exit 0
    }

    $packagePathFull = [IO.Path]::GetFullPath($PackagePath)
    $externalLocationFull = [IO.Path]::GetFullPath($ExternalLocation)
    $certificatePathFull = [IO.Path]::GetFullPath($CertificatePath)

    if (-not (Test-Path -LiteralPath $packagePathFull -PathType Leaf)) {
        throw "Package d'identité introuvable : $packagePathFull"
    }
    if (-not (Test-Path -LiteralPath $certificatePathFull -PathType Leaf)) {
        throw "Certificat public introuvable : $certificatePathFull"
    }
    if (-not (Test-Path -LiteralPath (Join-Path $externalLocationFull 'AtlasDrop.App.exe') -PathType Leaf)) {
        throw "AtlasDrop.App.exe introuvable dans l'emplacement externe."
    }

    # Nettoyer uniquement le certificat Atlas Drop précédemment mémorisé par notre installateur.
    if (Test-Path -LiteralPath $thumbprintPath) {
        $previousThumbprint = (Get-Content -LiteralPath $thumbprintPath -Raw).Trim()
        if ($previousThumbprint -match '^[0-9A-Fa-f]{40,64}$') {
            $previousCertificate = Get-ChildItem Cert:\CurrentUser\TrustedPeople |
                Where-Object { $_.Thumbprint -eq $previousThumbprint } |
                Select-Object -First 1
            if ($null -ne $previousCertificate) {
                Remove-Item -LiteralPath $previousCertificate.PSPath -Force
                Write-AtlasLog "Ancien certificat Atlas Drop retiré : $previousThumbprint"
            }
        }
    }

    $publicCertificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certificatePathFull)
    $thumbprint = $publicCertificate.Thumbprint
    $alreadyTrusted = Get-ChildItem Cert:\CurrentUser\TrustedPeople |
        Where-Object { $_.Thumbprint -eq $thumbprint } |
        Select-Object -First 1

    if ($null -eq $alreadyTrusted) {
        Import-Certificate -FilePath $certificatePathFull -CertStoreLocation Cert:\CurrentUser\TrustedPeople | Out-Null
        Write-AtlasLog "Certificat public Atlas Drop approuvé pour l'utilisateur courant : $thumbprint"
    }

    Set-Content -LiteralPath $thumbprintPath -Value $thumbprint -Encoding ASCII

    # Une même version sparse ne peut pas être réenregistrée sans retrait préalable.
    Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue |
        ForEach-Object {
            Remove-AppxPackage -Package $_.PackageFullName -ErrorAction Stop
            Write-AtlasLog "Ancienne identité retirée : $($_.PackageFullName)"
        }

    Add-AppxPackage -Path $packagePathFull -ExternalLocation $externalLocationFull -ErrorAction Stop

    $registered = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $registered) {
        throw "Windows n'a pas confirmé l'enregistrement de $packageName."
    }

    Write-AtlasLog "Menu contextuel moderne enregistré : $($registered.PackageFullName)"
    exit 0
}
catch {
    Write-AtlasLog "ECHEC menu moderne : $($_.Exception.Message)"
    # Le menu historique reste enregistré par l'installateur : ne pas rendre Atlas Drop inutilisable.
    exit 0
}
