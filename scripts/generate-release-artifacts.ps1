param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Repository = "MaximilianoVeiga/Lilith-Awakened-Mod",

    [string]$PackagesDir = "",

    [string]$InstallerPath = "",

    [string]$OutputDir = "",

    [string]$PackageConfig = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $PackagesDir) { $PackagesDir = Join-Path $repoRoot "packages" }
if (-not $InstallerPath) { $InstallerPath = Join-Path $repoRoot "artifacts\installer\LilithAI-Mod-Setup.exe" }
if (-not $OutputDir) { $OutputDir = Join-Path $repoRoot "artifacts\release" }
if (-not $PackageConfig) { $PackageConfig = Join-Path $repoRoot "release-packages.json" }

$Version = $Version.TrimStart("v", "V")
$tag = "v$Version"
$releaseBase = "https://github.com/$Repository/releases/download"

if (-not (Test-Path $PackageConfig)) {
    throw "Package config not found: $PackageConfig"
}

$config = Get-Content -LiteralPath $PackageConfig -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $config.packages) {
    throw "Package config does not contain a 'packages' object: $PackageConfig"
}

function Get-Sha256Lower([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-LocalRepoManifest {
    $localManifestPath = Join-Path $repoRoot "release-manifest.json"
    if (-not (Test-Path -LiteralPath $localManifestPath)) {
        return $null
    }
    return Get-Content -LiteralPath $localManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Get-ReuseManifest([string]$ReuseTag) {
    $url = "$releaseBase/$ReuseTag/release-manifest.json"
    Write-Host "Fetching package metadata from $url"
    try {
        return Invoke-RestMethod -Uri $url -Method Get
    }
    catch {
        Write-Warning "Remote manifest for '$ReuseTag' unavailable ($($_.Exception.Message)). Falling back to local release-manifest.json."
        $local = Get-LocalRepoManifest
        if ($null -eq $local) {
            throw "Failed to resolve reuse metadata for tag '$ReuseTag': remote manifest missing and local release-manifest.json not found."
        }
        return $local
    }
}

function Resolve-PackageFromSpec($Name, $Spec, $ReuseTag, $RemotePackage) {
    $fileName = if ($RemotePackage -and $RemotePackage.file) { [string]$RemotePackage.file } else { [string]$Spec.file }
    $sha = $null
    $bytes = $null
    $url = $null

    if ($Spec.sha256) { $sha = ([string]$Spec.sha256).ToUpperInvariant() }
    elseif ($RemotePackage -and $RemotePackage.sha256) { $sha = ([string]$RemotePackage.sha256).ToUpperInvariant() }

    if ($null -ne $Spec.bytes) { $bytes = [int64]$Spec.bytes }
    elseif ($RemotePackage -and $null -ne $RemotePackage.bytes) { $bytes = [int64]$RemotePackage.bytes }

    if ($Spec.url) { $url = [string]$Spec.url }
    elseif ($RemotePackage -and $RemotePackage.url) { $url = [string]$RemotePackage.url }
    else { $url = "$releaseBase/$ReuseTag/$fileName" }

    if (-not $sha -or $null -eq $bytes) {
        throw "Package '$Name' reuse metadata is incomplete (need sha256 and bytes via remote/local manifest or release-packages.json)."
    }

    return [ordered]@{
        file   = $fileName
        url    = $url
        sha256 = $sha
        bytes  = $bytes
    }
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$uploadDir = Join-Path $OutputDir "upload"
New-Item -ItemType Directory -Force -Path $uploadDir | Out-Null

$manifestPackages = [ordered]@{}
$sumsLines = New-Object System.Collections.Generic.List[string]
$uploadFiles = New-Object System.Collections.Generic.List[string]
$remoteManifestCache = @{}

if (Test-Path -LiteralPath $InstallerPath) {
    $installerName = Split-Path -Leaf $InstallerPath
    $installerHash = Get-Sha256Lower $InstallerPath
    $sumsLines.Add("$installerHash *$installerName")
    Copy-Item -LiteralPath $InstallerPath -Destination (Join-Path $uploadDir $installerName) -Force
    $uploadFiles.Add((Join-Path $uploadDir $installerName))
    Write-Host "Hashed installer: $installerName"
}
else {
    Write-Warning "Installer not found at '$InstallerPath'; SHA256SUMS will omit it."
}

foreach ($entry in $config.packages.PSObject.Properties) {
    $name = $entry.Name
    $spec = $entry.Value
    if (-not $spec.file) {
        throw "Package '$name' is missing required 'file' in $PackageConfig"
    }

    $fileName = [string]$spec.file
    $localPath = Join-Path $PackagesDir $fileName
    $package = $null

    if ($spec.url -and $spec.sha256 -and ($null -ne $spec.bytes) -and ([string]$spec.url -notlike "$releaseBase/*")) {
        $package = [ordered]@{
            file   = $fileName
            url    = [string]$spec.url
            sha256 = ([string]$spec.sha256).ToUpperInvariant()
            bytes  = [int64]$spec.bytes
        }
        Write-Host "Using external package URL for '$name': $($package.url)"
    }
    elseif (Test-Path -LiteralPath $localPath) {
        $sha = Get-Sha256Lower $localPath
        $bytes = (Get-Item -LiteralPath $localPath).Length
        $package = [ordered]@{
            file   = $fileName
            url    = if ($spec.url) { [string]$spec.url } else { "$releaseBase/$tag/$fileName" }
            sha256 = $sha.ToUpperInvariant()
            bytes  = $bytes
        }
        Copy-Item -LiteralPath $localPath -Destination (Join-Path $uploadDir $fileName) -Force
        $uploadFiles.Add((Join-Path $uploadDir $fileName))
        Write-Host "Using local package '$name' ($fileName)"
    }
    elseif ($spec.reuseFromTag) {
        $reuseTag = [string]$spec.reuseFromTag
        $remotePackage = $null
        if ($spec.sha256 -and $null -ne $spec.bytes) {
            Write-Host "Reusing package '$name' from $reuseTag (metadata pinned in release-packages.json)"
        }
        else {
            if (-not $remoteManifestCache.ContainsKey($reuseTag)) {
                $remoteManifestCache[$reuseTag] = Get-ReuseManifest $reuseTag
            }
            $remote = $remoteManifestCache[$reuseTag]
            $remotePackage = $remote.packages.$name
            if (-not $remotePackage -and -not $spec.sha256) {
                throw "Package '$name' was not found in release-manifest.json for tag '$reuseTag'."
            }
            Write-Host "Reusing package '$name' from $reuseTag"
        }
        $package = Resolve-PackageFromSpec $name $spec $reuseTag $remotePackage
    }
    else {
        throw @"
Package '$name' ($fileName) was not found in '$PackagesDir' and has no url+sha256+bytes or reuseFromTag.
Place the zip under packages/, pin an external URL in release-packages.json, or set reuseFromTag.
"@
    }

    $manifestPackages[$name] = $package
    $sumsLines.Add("$($package.sha256.ToLowerInvariant()) *packages/$($package.file)")
}

$manifest = [ordered]@{
    version  = $Version
    packages = $manifestPackages
}

$manifestPath = Join-Path $OutputDir "release-manifest.json"
$sumsPath = Join-Path $OutputDir "SHA256SUMS.txt"
$uploadListPath = Join-Path $OutputDir "upload-files.txt"

$manifestJson = $manifest | ConvertTo-Json -Depth 6
# Keep stable UTF-8 without BOM for release assets and installer parsing.
[System.IO.File]::WriteAllText($manifestPath, $manifestJson + "`n", [System.Text.UTF8Encoding]::new($false))
[System.IO.File]::WriteAllText($sumsPath, ($sumsLines -join "`n") + "`n", [System.Text.UTF8Encoding]::new($false))
[System.IO.File]::WriteAllText($uploadListPath, ($uploadFiles -join "`n") + "`n", [System.Text.UTF8Encoding]::new($false))

Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $uploadDir "release-manifest.json") -Force
Copy-Item -LiteralPath $sumsPath -Destination (Join-Path $uploadDir "SHA256SUMS.txt") -Force

Write-Host "Wrote $manifestPath"
Write-Host "Wrote $sumsPath"
Write-Host "Upload payload directory: $uploadDir"
