param(
    [string]$PackagesDir = "",
    [string]$CacheDir = "",
    [string]$ReleaseAssetsDir = "",
    [string]$StagingDir = "",
    [switch]$SkipCore,
    [switch]$SkipVoicePack,
    [switch]$SkipVoiceRuntime
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $PackagesDir) { $PackagesDir = Join-Path $repoRoot "packages" }
if (-not $CacheDir) { $CacheDir = Join-Path $repoRoot "artifacts\cache" }
if (-not $ReleaseAssetsDir) { $ReleaseAssetsDir = Join-Path $repoRoot "release-assets" }
if (-not $StagingDir) { $StagingDir = Join-Path $repoRoot "artifacts\staging" }

# Pinned upstream versions (see THIRD_PARTY_NOTICES.md)
$BepInExBuild = 780
$BepInExHash = "3be4532"
$BepInExUrl = "https://builds.bepinex.dev/projects/bepinex_be/$BepInExBuild/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.$BepInExBuild%2B$BepInExHash.zip"
$UvVersion = "0.11.14"
$UvUrl = "https://github.com/astral-sh/uv/releases/download/$UvVersion/uv-x86_64-pc-windows-msvc.zip"
$GptSoVitsCommit = "be6a4f1e9d8a22d41b7d42c22df9d7ef36f225d2"
$GptSoVitsUrl = "https://github.com/RVC-Boss/GPT-SoVITS/archive/$GptSoVitsCommit.zip"
$HfGptSoVits = "https://huggingface.co/lj1995/GPT-SoVITS/resolve/main"
$G2PwUrl = "https://huggingface.co/XXXXRT/GPT-SoVITS-Pretrained/resolve/main/G2PWModel.zip"
$VoiceAssetsTag = if ($env:VOICE_ASSETS_TAG) { $env:VOICE_ASSETS_TAG } else { "v1.0.0" }
$VoiceAssetsDefaultZip = "https://github.com/MaximilianoVeiga/Lilith-Awakened-Assets/releases/download/$VoiceAssetsTag/lilith-voice-assets.zip"
$VoiceAssetsUrl = if ($env:VOICE_ASSETS_URL) { $env:VOICE_ASSETS_URL } else { $VoiceAssetsDefaultZip }
$VoicePackUrl = $env:VOICE_PACK_URL
$VoiceModelsUrl = $env:VOICE_MODELS_URL

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message"
}

function Ensure-Dir([string]$Path) {
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Get-CachedFile {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [Parameter(Mandatory = $true)][string]$OutFile,
        [string]$OverrideUrl = ""
    )
    $effectiveUrl = if ($OverrideUrl) { $OverrideUrl } else { $Url }
    Ensure-Dir (Split-Path -Parent $OutFile)
    if (Test-Path -LiteralPath $OutFile) {
        Write-Host "Using cached file: $OutFile"
        return
    }
    Write-Host "Downloading $effectiveUrl"
    Write-Host "  -> $OutFile"
    Invoke-WebRequest -Uri $effectiveUrl -OutFile $OutFile -UseBasicParsing
}

function Expand-ZipTo([string]$ZipPath, [string]$Destination) {
    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Recurse -Force
    }
    Ensure-Dir $Destination
    Expand-Archive -LiteralPath $ZipPath -DestinationPath $Destination -Force
}

function Copy-DirectoryContents([string]$Source, [string]$Destination) {
    if (-not (Test-Path -LiteralPath $Source)) {
        throw "Source directory not found: $Source"
    }
    Ensure-Dir $Destination
    Copy-Item -Path (Join-Path $Source "*") -Destination $Destination -Recurse -Force
}

function Resolve-SevenZip {
    $candidates = @(
        $env:SEVEN_ZIP_EXE,
        (Join-Path ${env:ProgramFiles} "7-Zip\7z.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "7-Zip\7z.exe"),
        (Join-Path $repoRoot "artifacts\cache\7z\7z.exe")
    ) | Where-Object { $_ }
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c) { return $c }
    }
    $cmd = Get-Command 7z -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Get-DeflateCompressionLevel {
    $enum = [System.IO.Compression.CompressionLevel]
    $names = [Enum]::GetNames($enum)
    if ($names -contains "SmallestSize") {
        return [System.IO.Compression.CompressionLevel]::SmallestSize
    }
    return [System.IO.Compression.CompressionLevel]::Optimal
}

function New-ZipFromDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDir,
        [Parameter(Mandatory = $true)][string]$ZipPath
    )
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    if (Test-Path -LiteralPath $ZipPath) {
        Remove-Item -LiteralPath $ZipPath -Force
    }
    Ensure-Dir (Split-Path -Parent $ZipPath)

    $sourceFull = [System.IO.Path]::GetFullPath($SourceDir)
    $zipFull = [System.IO.Path]::GetFullPath($ZipPath)
    $sevenZip = Resolve-SevenZip

    if ($sevenZip) {
        Write-Host "Smart compress: 7-Zip ultra ($sevenZip)"
        # -tzip keeps installer-compatible ZIP; -mx=9 is maximum Deflate.
        # Working directory = staging root so entry paths stay game-relative.
        $args = @(
            "a", "-tzip", "-mx=9", "-mfb=258", "-mpass=15", "-y",
            $zipFull, "."
        )
        $p = Start-Process -FilePath $sevenZip -ArgumentList $args -WorkingDirectory $sourceFull -Wait -PassThru -NoNewWindow
        if ($p.ExitCode -ne 0) {
            throw "7-Zip failed with exit code $($p.ExitCode) while creating $zipFull"
        }
    }
    else {
        Write-Host "Smart compress: .NET max Deflate (install 7-Zip for better ratios)"
        $level = Get-DeflateCompressionLevel
        $zip = [System.IO.Compression.ZipFile]::Open($zipFull, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            # Include empty directories that matter for BepInEx layout.
            Get-ChildItem -LiteralPath $sourceFull -Recurse -Directory | ForEach-Object {
                $rel = $_.FullName.Substring($sourceFull.Length).TrimStart("\", "/")
                if (-not $rel) { return }
                $entryName = ($rel -replace "/", "\") + "\"
                $files = Get-ChildItem -LiteralPath $_.FullName -File -ErrorAction SilentlyContinue
                $dirs = Get-ChildItem -LiteralPath $_.FullName -Directory -ErrorAction SilentlyContinue
                if ((-not $files -or $files.Count -eq 0) -and (-not $dirs -or $dirs.Count -eq 0)) {
                    [void]$zip.CreateEntry($entryName)
                }
            }

            Get-ChildItem -LiteralPath $sourceFull -Recurse -File | ForEach-Object {
                $rel = $_.FullName.Substring($sourceFull.Length).TrimStart("\", "/")
                $entryName = $rel -replace "/", "\"
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                    $zip,
                    $_.FullName,
                    $entryName,
                    $level
                ) | Out-Null
            }
        }
        finally {
            $zip.Dispose()
        }
    }

    $size = (Get-Item -LiteralPath $zipFull).Length
    Write-Host ("Wrote {0} ({1:N1} MB)" -f $zipFull, ($size / 1MB))
}

function Test-VoiceAssetsCheckout([string]$Root) {
    $pack = Join-Path $Root "voice-pack\native-voice-pack"
    $models = Join-Path $Root "voice-models\lilith-e15.ckpt"
    return ((Test-Path -LiteralPath $pack) -and (Test-Path -LiteralPath $models))
}

function Get-VoiceAssetsRoot {
    $candidates = @(
        (Join-Path $repoRoot "Lilith-Awakened-Assets"),
        (Join-Path (Split-Path -Parent $repoRoot) "Lilith-Awakened-Assets")
    )
    foreach ($c in $candidates) {
        if (Test-VoiceAssetsCheckout $c) {
            return $c
        }
    }
    return $null
}

function Sync-VoiceAssetsFromLocalCheckout {
    $voiceAssetsRoot = Get-VoiceAssetsRoot
    if (-not $voiceAssetsRoot) {
        return $false
    }

    Write-Host "Using local voice assets checkout: $voiceAssetsRoot"
    $packDest = Join-Path $ReleaseAssetsDir "voice-pack"
    $modelsDest = Join-Path $ReleaseAssetsDir "voice-models"
    if (Test-Path $packDest) { Remove-Item $packDest -Recurse -Force }
    if (Test-Path $modelsDest) { Remove-Item $modelsDest -Recurse -Force }
    Ensure-Dir $packDest
    Ensure-Dir $modelsDest
    Copy-Item -LiteralPath (Join-Path $voiceAssetsRoot "voice-pack\native-voice-pack") -Destination (Join-Path $packDest "native-voice-pack") -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $voiceAssetsRoot "voice-pack\native-voice-pack-ja") -Destination (Join-Path $packDest "native-voice-pack-ja") -Recurse -Force
    Copy-DirectoryContents -Source (Join-Path $voiceAssetsRoot "voice-models") -Destination $modelsDest
    return $true
}

function Import-VoiceAssetZips {
    $combined = $VoiceAssetsUrl
    $packUrl = $VoicePackUrl
    $modelsUrl = $VoiceModelsUrl
    # Prefer explicit split URLs when both are set; otherwise use combined (default: Assets v1.0.0 release).
    # When -SkipVoicePack, models-only URL is enough.
    $useSplit = ($packUrl -and $modelsUrl)
    $useModelsOnly = $SkipVoicePack -and $modelsUrl -and -not $packUrl
    if (-not $useSplit -and -not $useModelsOnly -and -not $combined) {
        return $false
    }

    Write-Step "Fetching voice assets from Lilith-Awakened-Assets release"
    $packDest = Join-Path $ReleaseAssetsDir "voice-pack"
    $modelsDest = Join-Path $ReleaseAssetsDir "voice-models"
    Ensure-Dir $packDest
    Ensure-Dir $modelsDest

    if ($useModelsOnly) {
        $modelsZip = Join-Path $CacheDir "voice-models-$VoiceAssetsTag.zip"
        Get-CachedFile -Url $modelsUrl -OutFile $modelsZip
        $modelsExtract = Join-Path $StagingDir "voice-models-remote"
        Expand-ZipTo $modelsZip $modelsExtract
        if (Test-Path $modelsDest) { Remove-Item $modelsDest -Recurse -Force }
        Ensure-Dir $modelsDest
        $ckpt = Get-ChildItem -LiteralPath $modelsExtract -Recurse -Filter "lilith-e15.ckpt" | Select-Object -First 1
        $pth = Get-ChildItem -LiteralPath $modelsExtract -Recurse -Filter "lilith_e8_s288.pth" | Select-Object -First 1
        if (-not $ckpt -or -not $pth) {
            throw "VOICE_MODELS_URL zip must contain lilith-e15.ckpt and lilith_e8_s288.pth."
        }
        Copy-Item -LiteralPath $ckpt.FullName -Destination (Join-Path $modelsDest "lilith-e15.ckpt") -Force
        Copy-Item -LiteralPath $pth.FullName -Destination (Join-Path $modelsDest "lilith_e8_s288.pth") -Force
        return $true
    }

    if (-not $useSplit) {
        $zip = Join-Path $CacheDir "lilith-voice-assets-$VoiceAssetsTag.zip"
        Get-CachedFile -Url $combined -OutFile $zip
        $extract = Join-Path $StagingDir "voice-assets-combined"
        Expand-ZipTo $zip $extract

        if (-not $SkipVoicePack) {
            if (Test-Path $packDest) { Remove-Item $packDest -Recurse -Force }
            Ensure-Dir $packDest
            $packRoot = if (Test-Path (Join-Path $extract "voice-pack\native-voice-pack")) {
                Join-Path $extract "voice-pack"
            }
            elseif (Test-Path (Join-Path $extract "native-voice-pack")) {
                $extract
            }
            else {
                throw "Voice assets zip must contain voice-pack/native-voice-pack or native-voice-pack at its root: $combined"
            }
            Copy-Item -LiteralPath (Join-Path $packRoot "native-voice-pack") -Destination (Join-Path $packDest "native-voice-pack") -Recurse -Force
            Copy-Item -LiteralPath (Join-Path $packRoot "native-voice-pack-ja") -Destination (Join-Path $packDest "native-voice-pack-ja") -Recurse -Force
        }

        if (Test-Path $modelsDest) { Remove-Item $modelsDest -Recurse -Force }
        Ensure-Dir $modelsDest
        $ckpt = Get-ChildItem -LiteralPath $extract -Recurse -Filter "lilith-e15.ckpt" | Select-Object -First 1
        $pth = Get-ChildItem -LiteralPath $extract -Recurse -Filter "lilith_e8_s288.pth" | Select-Object -First 1
        if (-not $ckpt -or -not $pth) {
            throw "Voice assets zip must contain lilith-e15.ckpt and lilith_e8_s288.pth: $combined"
        }
        Copy-Item -LiteralPath $ckpt.FullName -Destination (Join-Path $modelsDest "lilith-e15.ckpt") -Force
        Copy-Item -LiteralPath $pth.FullName -Destination (Join-Path $modelsDest "lilith_e8_s288.pth") -Force
        return $true
    }

    $packZip = Join-Path $CacheDir "voice-pack-$VoiceAssetsTag.zip"
    $modelsZip = Join-Path $CacheDir "voice-models-$VoiceAssetsTag.zip"
    if (-not $SkipVoicePack) {
        Get-CachedFile -Url $packUrl -OutFile $packZip
    }
    Get-CachedFile -Url $modelsUrl -OutFile $modelsZip

    $modelsExtract = Join-Path $StagingDir "voice-models-remote"
    Expand-ZipTo $modelsZip $modelsExtract

    if (-not $SkipVoicePack) {
        $packExtract = Join-Path $StagingDir "voice-pack-remote"
        Expand-ZipTo $packZip $packExtract
        if (Test-Path $packDest) { Remove-Item $packDest -Recurse -Force }
        Ensure-Dir $packDest
        if (Test-Path (Join-Path $packExtract "native-voice-pack")) {
            Copy-Item -LiteralPath (Join-Path $packExtract "native-voice-pack") -Destination (Join-Path $packDest "native-voice-pack") -Recurse -Force
            Copy-Item -LiteralPath (Join-Path $packExtract "native-voice-pack-ja") -Destination (Join-Path $packDest "native-voice-pack-ja") -Recurse -Force
        }
        else {
            throw "VOICE_PACK_URL zip must contain native-voice-pack/ and native-voice-pack-ja/ at its root."
        }
    }

    if (Test-Path $modelsDest) { Remove-Item $modelsDest -Recurse -Force }
    Ensure-Dir $modelsDest
    $ckpt = Get-ChildItem -LiteralPath $modelsExtract -Recurse -Filter "lilith-e15.ckpt" | Select-Object -First 1
    $pth = Get-ChildItem -LiteralPath $modelsExtract -Recurse -Filter "lilith_e8_s288.pth" | Select-Object -First 1
    if (-not $ckpt -or -not $pth) {
        throw "VOICE_MODELS_URL zip must contain lilith-e15.ckpt and lilith_e8_s288.pth."
    }
    Copy-Item -LiteralPath $ckpt.FullName -Destination (Join-Path $modelsDest "lilith-e15.ckpt") -Force
    Copy-Item -LiteralPath $pth.FullName -Destination (Join-Path $modelsDest "lilith_e8_s288.pth") -Force
    return $true
}

function Test-IsGitLfsPointer([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }
    $info = Get-Item -LiteralPath $Path
    # Real media/model blobs are large; LFS pointer files are tiny text stubs.
    if ($info.Length -gt 1024) {
        return $false
    }
    try {
        $head = Get-Content -LiteralPath $Path -TotalCount 1 -ErrorAction Stop
        return ($head -like "version https://git-lfs.github.com/spec/v1*")
    }
    catch {
        return $false
    }
}

function Assert-RealBlob([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing $Label`: $Path"
    }
    if (Test-IsGitLfsPointer $Path) {
        throw "$Label is still a Git LFS pointer (not smudged): $Path. Run 'git lfs pull' or re-download voice assets ($VoiceAssetsDefaultZip)."
    }
}

function Test-ReleaseAssetsNeedMirror {
    $probes = @(
        (Join-Path $ReleaseAssetsDir "core-voice\calm-reference.wav"),
        (Join-Path $ReleaseAssetsDir "unity-libs\UnityEngine.CoreModule.dll")
    )
    foreach ($p in $probes) {
        if (-not (Test-Path -LiteralPath $p)) { return $true }
        if (Test-IsGitLfsPointer $p) { return $true }
    }
    return $false
}

function Test-VoiceModelsReady {
    $ckpt = Join-Path $ReleaseAssetsDir "voice-models\lilith-e15.ckpt"
    $pth = Join-Path $ReleaseAssetsDir "voice-models\lilith_e8_s288.pth"
    if (-not ((Test-Path $ckpt) -and (Test-Path $pth))) { return $false }
    if ((Test-IsGitLfsPointer $ckpt) -or (Test-IsGitLfsPointer $pth)) { return $false }
    return $true
}

function Test-VoicePackReady {
    $zh = Join-Path $ReleaseAssetsDir "voice-pack\native-voice-pack"
    $ja = Join-Path $ReleaseAssetsDir "voice-pack\native-voice-pack-ja"
    if (-not ((Test-Path $zh) -and (Test-Path $ja))) { return $false }
    $zhWav = Get-ChildItem -LiteralPath $zh -Filter "*.wav" -File -ErrorAction SilentlyContinue | Select-Object -First 1
    $jaWav = Get-ChildItem -LiteralPath $ja -Filter "*.wav" -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $zhWav -or -not $jaWav) { return $false }
    if ((Test-IsGitLfsPointer $zhWav.FullName) -or (Test-IsGitLfsPointer $jaWav.FullName)) { return $false }
    return $true
}

function Test-VoiceAssetsReady {
    if (-not (Test-VoiceModelsReady)) { return $false }
    if ($SkipVoicePack) { return $true }
    return (Test-VoicePackReady)
}

function Import-ReleaseAssetsMirror {
    if (-not $env:RELEASE_ASSETS_URL) {
        return
    }
    Write-Step "Fetching release-assets mirror from RELEASE_ASSETS_URL"
    $mirrorZip = Join-Path $CacheDir "release-assets-mirror.zip"
    if (Test-Path -LiteralPath $mirrorZip) {
        Remove-Item -LiteralPath $mirrorZip -Force
    }
    Get-CachedFile -Url $env:RELEASE_ASSETS_URL -OutFile $mirrorZip -OverrideUrl $env:RELEASE_ASSETS_URL
    $mirrorExtract = Join-Path $StagingDir "release-assets-mirror"
    Expand-ZipTo $mirrorZip $mirrorExtract
    Ensure-Dir $ReleaseAssetsDir
    Copy-DirectoryContents $mirrorExtract $ReleaseAssetsDir
}

function Resolve-ReleaseAssets {
    # 1) Optional local sibling/nested assets checkout (for maintainers)
    if (-not (Test-VoiceAssetsReady)) {
        [void](Sync-VoiceAssetsFromLocalCheckout)
    }
    # 2) Download Lilith-Awakened-Assets release zip (default: v1.0.0 lilith-voice-assets.zip)
    if (-not (Test-VoiceAssetsReady)) {
        [void](Import-VoiceAssetZips)
    }

    $needsMirror = Test-ReleaseAssetsNeedMirror
    if ($needsMirror) {
        if ($env:RELEASE_ASSETS_URL) {
            Import-ReleaseAssetsMirror
        }
        elseif (-not (Test-Path (Join-Path $ReleaseAssetsDir "core-voice"))) {
            throw "release-assets/ is incomplete and RELEASE_ASSETS_URL is not set."
        }
    }

    if (-not (Test-VoiceAssetsReady)) {
        throw @"
Voice pack/models are missing.
Failed to download $VoiceAssetsDefaultZip (override with VOICE_ASSETS_URL or VOICE_PACK_URL + VOICE_MODELS_URL).
See https://github.com/MaximilianoVeiga/Lilith-Awakened-Assets/releases/tag/$VoiceAssetsTag
"@
    }

    $required = @(
        "core-voice",
        "voice-models\lilith-e15.ckpt",
        "voice-models\lilith_e8_s288.pth",
        "unity-libs",
        "voice-runtime\requirements-inference.txt",
        "voice-runtime\config"
    )
    if (-not $SkipVoicePack) {
        $required += @(
            "voice-pack\native-voice-pack",
            "voice-pack\native-voice-pack-ja"
        )
    }
    foreach ($rel in $required) {
        $path = Join-Path $ReleaseAssetsDir $rel
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Missing release-assets entry: $path"
        }
    }

    Assert-RealBlob (Join-Path $ReleaseAssetsDir "core-voice\calm-reference.wav") "core-voice WAV"
    Assert-RealBlob (Join-Path $ReleaseAssetsDir "voice-models\lilith-e15.ckpt") "voice model"
    Assert-RealBlob (Join-Path $ReleaseAssetsDir "voice-models\lilith_e8_s288.pth") "voice model"
    Assert-RealBlob (Join-Path $ReleaseAssetsDir "unity-libs\UnityEngine.CoreModule.dll") "unity-libs DLL"

    if (-not $SkipVoicePack) {
        $zhCount = @(Get-ChildItem -LiteralPath (Join-Path $ReleaseAssetsDir "voice-pack\native-voice-pack") -Filter "*.wav" -File -Recurse -ErrorAction SilentlyContinue).Count
        $jaCount = @(Get-ChildItem -LiteralPath (Join-Path $ReleaseAssetsDir "voice-pack\native-voice-pack-ja") -Filter "*.wav" -File -Recurse -ErrorAction SilentlyContinue).Count
        if ($zhCount -lt 1 -or $jaCount -lt 1) {
            throw "voice-pack assets incomplete (zh wavs=$zhCount, ja wavs=$jaCount)."
        }
        $sampleZh = Get-ChildItem -LiteralPath (Join-Path $ReleaseAssetsDir "voice-pack\native-voice-pack") -Filter "*.wav" -File -Recurse | Select-Object -First 1
        Assert-RealBlob $sampleZh.FullName "voice-pack WAV"
    }
}

function Get-GameInteropPath {
    if ($env:GameInterop -and (Test-Path (Join-Path $env:GameInterop "Assembly-CSharp.dll"))) {
        return $env:GameInterop
    }
    $fromAssets = Join-Path $ReleaseAssetsDir "game-interop"
    if (Test-Path (Join-Path $fromAssets "Assembly-CSharp.dll")) {
        return $fromAssets
    }
    if ($env:GAME_INTEROP_URL) {
        Write-Step "Fetching game interop from GAME_INTEROP_URL"
        $zip = Join-Path $CacheDir "game-interop.zip"
        if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
        Get-CachedFile -Url $env:GAME_INTEROP_URL -OutFile $zip -OverrideUrl $env:GAME_INTEROP_URL
        $extract = Join-Path $StagingDir "game-interop-remote"
        Expand-ZipTo $zip $extract
        $root = if (Test-Path (Join-Path $extract "Assembly-CSharp.dll")) {
            $extract
        }
        elseif (Test-Path (Join-Path $extract "interop\Assembly-CSharp.dll")) {
            Join-Path $extract "interop"
        }
        else {
            $found = Get-ChildItem -LiteralPath $extract -Recurse -Filter "Assembly-CSharp.dll" | Select-Object -First 1
            if (-not $found) {
                throw "GAME_INTEROP_URL zip must contain Assembly-CSharp.dll"
            }
            $found.DirectoryName
        }
        Ensure-Dir $fromAssets
        Copy-Item -Path (Join-Path $root "*") -Destination $fromAssets -Recurse -Force
        if (Test-Path (Join-Path $fromAssets "Assembly-CSharp.dll")) {
            return $fromAssets
        }
    }
    $steam = "C:\Program Files (x86)\Steam\steamapps\common\The NOexistenceN of Lilith\BepInEx\interop"
    if (Test-Path (Join-Path $steam "Assembly-CSharp.dll")) {
        return $steam
    }
    return $null
}

function Get-PrebuiltPluginDir {
    $local = Join-Path $ReleaseAssetsDir "prebuilt-plugins"
    if (Test-Path (Join-Path $local "LilithTextInjector.dll")) {
        return $local
    }
    if ($env:PREBUILT_CORE_PLUGINS_URL) {
        Write-Step "Fetching prebuilt core plugins from PREBUILT_CORE_PLUGINS_URL"
        $zip = Join-Path $CacheDir "prebuilt-core-plugins.zip"
        if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
        Get-CachedFile -Url $env:PREBUILT_CORE_PLUGINS_URL -OutFile $zip -OverrideUrl $env:PREBUILT_CORE_PLUGINS_URL
        $extract = Join-Path $StagingDir "prebuilt-plugins-remote"
        Expand-ZipTo $zip $extract
        $dll = Get-ChildItem -LiteralPath $extract -Recurse -Filter "LilithTextInjector.dll" | Select-Object -First 1
        if (-not $dll) {
            throw "PREBUILT_CORE_PLUGINS_URL zip must contain LilithTextInjector.dll"
        }
        return $dll.DirectoryName
    }
    $refPlugins = Join-Path $repoRoot "references\core\BepInEx\plugins"
    if (Test-Path (Join-Path $refPlugins "LilithTextInjector.dll")) {
        return $refPlugins
    }
    return $null
}

function Install-BepInExRefs([string]$BepInExCoreSource) {
    $dest = Join-Path $repoRoot "src\bepinex-be780\BepInEx\core"
    Ensure-Dir $dest
    Copy-Item -Path (Join-Path $BepInExCoreSource "*") -Destination $dest -Force
}

function Get-BepInExBaseLayout {
    $zipPath = Join-Path $CacheDir "BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.$BepInExBuild+$BepInExHash.zip"
    $override = $env:BEPINEX_ZIP_URL
    try {
        Get-CachedFile -Url $BepInExUrl -OutFile $zipPath -OverrideUrl $override
    }
    catch {
        Write-Warning "BepInEx download failed ($($_.Exception.Message)); trying references/core fallback."
        $refCore = Join-Path $repoRoot "references\core"
        if (-not (Test-Path $refCore)) {
            throw "Unable to obtain BepInEx base layout."
        }
        $fallback = Join-Path $StagingDir "bepinex-from-references"
        if (Test-Path $fallback) { Remove-Item $fallback -Recurse -Force }
        Ensure-Dir $fallback
        foreach ($name in @(".doorstop_version", "doorstop_config.ini", "winhttp.dll", "dotnet", "BepInEx")) {
            $src = Join-Path $refCore $name
            if (Test-Path $src) {
                Copy-Item -Path $src -Destination (Join-Path $fallback $name) -Recurse -Force
            }
        }
        # Strip our plugin/data from the reference tree.
        $plugins = Join-Path $fallback "BepInEx\plugins"
        if (Test-Path $plugins) { Remove-Item $plugins -Recurse -Force }
        $data = Join-Path $fallback "BepInEx\data"
        if (Test-Path $data) { Remove-Item $data -Recurse -Force }
        return $fallback
    }

    $extractRoot = Join-Path $StagingDir "bepinex-extract"
    Expand-ZipTo $zipPath $extractRoot

    # Upstream zip may nest a single root folder.
    $doorstop = Get-ChildItem -LiteralPath $extractRoot -Recurse -Filter "doorstop_config.ini" | Select-Object -First 1
    if (-not $doorstop) {
        throw "doorstop_config.ini not found inside BepInEx zip."
    }
    return $doorstop.Directory.FullName
}

function Build-Injector([string]$GameInterop, [string]$OutDir) {
    $project = Join-Path $repoRoot "src\LilithTextInjector\LilithTextInjector.csproj"
    Ensure-Dir $OutDir
    Write-Host "Building LilithTextInjector (GameInterop=$GameInterop)"
    & dotnet build $project -c Release -o $OutDir "-p:GameInterop=$GameInterop" --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build LilithTextInjector failed with exit code $LASTEXITCODE"
    }
    $dll = Join-Path $OutDir "LilithTextInjector.dll"
    if (-not (Test-Path $dll)) {
        throw "LilithTextInjector.dll was not produced at $dll"
    }
}

function Publish-VoiceHost([string]$OutDir) {
    $project = Join-Path $repoRoot "src\LilithVoiceHost\LilithVoiceHost.csproj"
    Ensure-Dir $OutDir
    Write-Host "Publishing LilithVoiceHost"
    & dotnet publish $project -c Release -o $OutDir --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish LilithVoiceHost failed with exit code $LASTEXITCODE"
    }
    $exe = Join-Path $OutDir "LilithVoiceHost.exe"
    if (-not (Test-Path $exe)) {
        throw "LilithVoiceHost.exe was not produced at $exe"
    }
}

function Get-UvExe([string]$DestinationExe) {
    $zipPath = Join-Path $CacheDir "uv-$UvVersion-windows.zip"
    Get-CachedFile -Url $UvUrl -OutFile $zipPath
    $extract = Join-Path $StagingDir "uv-extract"
    Expand-ZipTo $zipPath $extract
    $found = Get-ChildItem -LiteralPath $extract -Recurse -Filter "uv.exe" | Select-Object -First 1
    if (-not $found) {
        throw "uv.exe not found in $UvUrl archive"
    }
    Ensure-Dir (Split-Path -Parent $DestinationExe)
    Copy-Item -LiteralPath $found.FullName -Destination $DestinationExe -Force
}

function Get-GptSoVitsSource([string]$DestinationDir) {
    $refTree = Join-Path $repoRoot "references\voice-runtime\BepInEx\data\LilithTextInjector\voice-runtime\gpt-sovits"
    if (Test-Path (Join-Path $refTree "api_v2.py")) {
        Write-Host "Using GPT-SoVITS tree from references/ (local golden)"
        if (Test-Path $DestinationDir) { Remove-Item $DestinationDir -Recurse -Force }
        Ensure-Dir (Split-Path -Parent $DestinationDir)
        Copy-Item -LiteralPath $refTree -Destination $DestinationDir -Recurse -Force
        return
    }

    $zipPath = Join-Path $CacheDir "GPT-SoVITS-$GptSoVitsCommit.zip"
    Get-CachedFile -Url $GptSoVitsUrl -OutFile $zipPath
    $extract = Join-Path $StagingDir "gpt-sovits-src"
    Expand-ZipTo $zipPath $extract
    $inner = Get-ChildItem -LiteralPath $extract -Directory | Select-Object -First 1
    if (-not $inner) {
        throw "GPT-SoVITS archive had no root directory"
    }
    if (Test-Path $DestinationDir) { Remove-Item $DestinationDir -Recurse -Force }
    Ensure-Dir (Split-Path -Parent $DestinationDir)
    Move-Item -LiteralPath $inner.FullName -Destination $DestinationDir
}

function Get-CachedOrDownload([string]$Url, [string]$OutFile) {
    Get-CachedFile -Url $Url -OutFile $OutFile
}

function Ensure-PretrainedModels([string]$GptRoot) {
    $pretrained = Join-Path $GptRoot "GPT_SoVITS\pretrained_models"
    $hubertBin = Join-Path $pretrained "chinese-hubert-base\pytorch_model.bin"
    $robertaBin = Join-Path $pretrained "chinese-roberta-wwm-ext-large\pytorch_model.bin"
    $g2pwOnnx = Join-Path $GptRoot "GPT_SoVITS\text\G2PWModel\g2pW.onnx"

    $refPretrained = Join-Path $repoRoot "references\voice-runtime\BepInEx\data\LilithTextInjector\voice-runtime\gpt-sovits\GPT_SoVITS\pretrained_models"
    $refG2pw = Join-Path $repoRoot "references\voice-runtime\BepInEx\data\LilithTextInjector\voice-runtime\gpt-sovits\GPT_SoVITS\text\G2PWModel"

    if ((Test-Path $hubertBin) -and (Test-Path $robertaBin) -and (Test-Path $g2pwOnnx)) {
        Write-Host "Pretrained models already present under GPT-SoVITS tree"
        return
    }

    if (Test-Path $refPretrained) {
        Write-Host "Copying pretrained_models from references/"
        Ensure-Dir $pretrained
        Copy-DirectoryContents $refPretrained $pretrained
    }
    if ((-not (Test-Path $g2pwOnnx)) -and (Test-Path $refG2pw)) {
        Write-Host "Copying G2PWModel from references/"
        $destG2 = Join-Path $GptRoot "GPT_SoVITS\text\G2PWModel"
        Ensure-Dir (Split-Path -Parent $destG2)
        if (Test-Path $destG2) { Remove-Item $destG2 -Recurse -Force }
        Copy-Item -LiteralPath $refG2pw -Destination $destG2 -Recurse -Force
    }

    if ((Test-Path $hubertBin) -and (Test-Path $robertaBin) -and (Test-Path $g2pwOnnx)) {
        return
    }

    Write-Step "Downloading GPT-SoVITS pretrained weights from Hugging Face"
    $files = @(
        @{ Rel = "chinese-hubert-base/config.json"; Url = "$HfGptSoVits/chinese-hubert-base/config.json" },
        @{ Rel = "chinese-hubert-base/preprocessor_config.json"; Url = "$HfGptSoVits/chinese-hubert-base/preprocessor_config.json" },
        @{ Rel = "chinese-hubert-base/pytorch_model.bin"; Url = "$HfGptSoVits/chinese-hubert-base/pytorch_model.bin" },
        @{ Rel = "chinese-roberta-wwm-ext-large/config.json"; Url = "$HfGptSoVits/chinese-roberta-wwm-ext-large/config.json" },
        @{ Rel = "chinese-roberta-wwm-ext-large/tokenizer.json"; Url = "$HfGptSoVits/chinese-roberta-wwm-ext-large/tokenizer.json" },
        @{ Rel = "chinese-roberta-wwm-ext-large/pytorch_model.bin"; Url = "$HfGptSoVits/chinese-roberta-wwm-ext-large/pytorch_model.bin" },
        @{ Rel = "gsv-v2final-pretrained/s1bert25hz-5kh-longer-epoch=12-step=369668.ckpt"; Url = "$HfGptSoVits/gsv-v2final-pretrained/s1bert25hz-5kh-longer-epoch=12-step=369668.ckpt" },
        @{ Rel = "gsv-v2final-pretrained/s2D2333k.pth"; Url = "$HfGptSoVits/gsv-v2final-pretrained/s2D2333k.pth" },
        @{ Rel = "gsv-v2final-pretrained/s2G2333k.pth"; Url = "$HfGptSoVits/gsv-v2final-pretrained/s2G2333k.pth" },
        @{ Rel = "fast_langdetect/lid.176.bin"; Url = "$HfGptSoVits/fast_langdetect/lid.176.bin" }
    )

    foreach ($f in $files) {
        $out = Join-Path $pretrained ($f.Rel -replace "/", "\")
        $cacheFile = Join-Path $CacheDir ("gpt-sovits-pretrained\" + ($f.Rel -replace "/", "\"))
        Get-CachedOrDownload -Url $f.Url -OutFile $cacheFile
        Ensure-Dir (Split-Path -Parent $out)
        Copy-Item -LiteralPath $cacheFile -Destination $out -Force
    }

    if (-not (Test-Path $g2pwOnnx)) {
        $g2Zip = Join-Path $CacheDir "G2PWModel.zip"
        Get-CachedOrDownload -Url $G2PwUrl -OutFile $g2Zip
        $g2Extract = Join-Path $StagingDir "g2pw-extract"
        Expand-ZipTo $g2Zip $g2Extract
        $modelDir = Join-Path $GptRoot "GPT_SoVITS\text\G2PWModel"
        if (Test-Path $modelDir) { Remove-Item $modelDir -Recurse -Force }
        Ensure-Dir (Split-Path -Parent $modelDir)
        $inner = Get-ChildItem -LiteralPath $g2Extract -Directory | Select-Object -First 1
        if ($inner) {
            Move-Item -LiteralPath $inner.FullName -Destination $modelDir
        }
        else {
            # Zip may already be flat files named G2PWModel/*
            $flat = Join-Path $g2Extract "g2pW.onnx"
            if (Test-Path $flat) {
                Ensure-Dir $modelDir
                Copy-Item -Path (Join-Path $g2Extract "*") -Destination $modelDir -Recurse -Force
            }
            else {
                throw "Could not locate G2PWModel contents in $g2Zip"
            }
        }
    }

    if (-not ((Test-Path $hubertBin) -and (Test-Path $robertaBin) -and (Test-Path $g2pwOnnx))) {
        throw "GPT-SoVITS pretrained models are incomplete after download."
    }
}

function Pack-Core {
    Write-Step "Packing core.zip"
    $stage = Join-Path $StagingDir "core"
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    Ensure-Dir $stage

    $bepRoot = Get-BepInExBaseLayout
    Write-Host "BepInEx base: $bepRoot"
    Copy-DirectoryContents $bepRoot $stage

    $unityLibsDest = Join-Path $stage "BepInEx\unity-libs"
    if (Test-Path $unityLibsDest) { Remove-Item $unityLibsDest -Recurse -Force }
    Copy-Item -LiteralPath (Join-Path $ReleaseAssetsDir "unity-libs") -Destination $unityLibsDest -Recurse -Force

    $patchers = Join-Path $stage "BepInEx\patchers"
    Ensure-Dir $patchers

    $coreDlls = Join-Path $stage "BepInEx\core"
    Install-BepInExRefs $coreDlls

    $plugins = Join-Path $stage "BepInEx\plugins"
    Ensure-Dir $plugins

    $interop = Get-GameInteropPath
    $buildOut = Join-Path $StagingDir "injector-build"
    $usedFallbackDll = $false
    if ($interop) {
        Build-Injector -GameInterop $interop -OutDir $buildOut
        Copy-Item -LiteralPath (Join-Path $buildOut "LilithTextInjector.dll") -Destination $plugins -Force
        foreach ($dep in @("NAudio.dll", "NAudio.Core.dll", "NAudio.Wasapi.dll")) {
            $depPath = Join-Path $buildOut $dep
            if (-not (Test-Path $depPath)) {
                throw "Injector build missing dependency: $dep"
            }
            Copy-Item -LiteralPath $depPath -Destination $plugins -Force
        }
    }
    else {
        $prebuilt = Get-PrebuiltPluginDir
        if (-not $prebuilt) {
            throw "Game interop not found and no prebuilt plugins available. Populate release-assets/game-interop, set GAME_INTEROP_URL, or add release-assets/prebuilt-plugins/LilithTextInjector.dll."
        }
        Write-Warning "Game interop not found; using prebuilt plugins from $prebuilt"
        foreach ($name in @("LilithTextInjector.dll", "NAudio.dll", "NAudio.Core.dll", "NAudio.Wasapi.dll")) {
            $src = Join-Path $prebuilt $name
            if (-not (Test-Path $src)) {
                throw "Prebuilt plugin missing: $src"
            }
            Copy-Item -LiteralPath $src -Destination (Join-Path $plugins $name) -Force
        }
        $usedFallbackDll = $true
    }

    $voiceDest = Join-Path $stage "BepInEx\data\LilithTextInjector\voice"
    if (Test-Path $voiceDest) { Remove-Item $voiceDest -Recurse -Force }
    Ensure-Dir (Split-Path -Parent $voiceDest)
    Copy-Item -LiteralPath (Join-Path $ReleaseAssetsDir "core-voice") -Destination $voiceDest -Recurse -Force

    # Drop any accidental config/log leftovers from upstream extract.
    $cfg = Join-Path $stage "BepInEx\config"
    if (Test-Path $cfg) { Remove-Item $cfg -Recurse -Force }
    $log = Join-Path $stage "BepInEx\LogOutput.txt"
    if (Test-Path $log) { Remove-Item $log -Force }

    $required = @(
        "winhttp.dll",
        "doorstop_config.ini",
        ".doorstop_version",
        "dotnet\coreclr.dll",
        "BepInEx\core\BepInEx.Unity.IL2CPP.dll",
        "BepInEx\unity-libs\UnityEngine.CoreModule.dll",
        "BepInEx\unity-libs\UnityEngine.dll",
        "BepInEx\plugins\LilithTextInjector.dll",
        "BepInEx\plugins\NAudio.dll",
        "BepInEx\plugins\NAudio.Core.dll",
        "BepInEx\plugins\NAudio.Wasapi.dll",
        "BepInEx\data\LilithTextInjector\voice\calm-reference.wav"
    )
    foreach ($rel in $required) {
        if (-not (Test-Path (Join-Path $stage $rel))) {
            throw "core.zip staging missing required path: $rel"
        }
    }
    Assert-RealBlob (Join-Path $stage "BepInEx\data\LilithTextInjector\voice\calm-reference.wav") "staged core-voice WAV"

    Ensure-Dir $PackagesDir
    New-ZipFromDirectory -SourceDir $stage -ZipPath (Join-Path $PackagesDir "core.zip")
    if ($usedFallbackDll) {
        Write-Warning "core.zip used a prebuilt injector DLL fallback (interop missing)."
    }
}

function Pack-VoicePack {
    Write-Step "Packing voice-pack.zip"
    $stage = Join-Path $StagingDir "voice-pack"
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    $destRoot = Join-Path $stage "BepInEx\data\LilithTextInjector"
    Ensure-Dir $destRoot
    Copy-Item -LiteralPath (Join-Path $ReleaseAssetsDir "voice-pack\native-voice-pack") -Destination (Join-Path $destRoot "native-voice-pack") -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $ReleaseAssetsDir "voice-pack\native-voice-pack-ja") -Destination (Join-Path $destRoot "native-voice-pack-ja") -Recurse -Force

    $zhWavs = @(Get-ChildItem -LiteralPath (Join-Path $destRoot "native-voice-pack") -Filter "*.wav" -File -Recurse -ErrorAction SilentlyContinue)
    $jaWavs = @(Get-ChildItem -LiteralPath (Join-Path $destRoot "native-voice-pack-ja") -Filter "*.wav" -File -Recurse -ErrorAction SilentlyContinue)
    if ($zhWavs.Count -lt 1 -or $jaWavs.Count -lt 1) {
        throw "voice-pack staging incomplete (zh wavs=$($zhWavs.Count), ja wavs=$($jaWavs.Count)); manifest-only packs are not allowed."
    }
    Assert-RealBlob $zhWavs[0].FullName "staged voice-pack WAV"
    Assert-RealBlob $jaWavs[0].FullName "staged voice-pack-ja WAV"
    Write-Host "voice-pack wav counts: zh=$($zhWavs.Count) ja=$($jaWavs.Count)"

    Ensure-Dir $PackagesDir
    New-ZipFromDirectory -SourceDir $stage -ZipPath (Join-Path $PackagesDir "voice-pack.zip")
}

function Pack-VoiceRuntime {
    Write-Step "Packing voice-runtime.zip"
    $stage = Join-Path $StagingDir "voice-runtime"
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    $runtime = Join-Path $stage "BepInEx\data\LilithTextInjector\voice-runtime"
    Ensure-Dir $runtime

    $hostOut = Join-Path $StagingDir "voicehost-publish"
    Publish-VoiceHost -OutDir $hostOut
    Copy-Item -LiteralPath (Join-Path $hostOut "LilithVoiceHost.exe") -Destination (Join-Path $runtime "LilithVoiceHost.exe") -Force

    Get-UvExe -DestinationExe (Join-Path $runtime "uv.exe")

    Copy-Item -LiteralPath (Join-Path $ReleaseAssetsDir "voice-runtime\requirements-inference.txt") -Destination $runtime -Force
    $configDest = Join-Path $runtime "config"
    if (Test-Path $configDest) { Remove-Item $configDest -Recurse -Force }
    Copy-Item -LiteralPath (Join-Path $ReleaseAssetsDir "voice-runtime\config") -Destination $configDest -Recurse -Force

    $gptDir = Join-Path $runtime "gpt-sovits"
    Get-GptSoVitsSource -DestinationDir $gptDir
    Ensure-PretrainedModels -GptRoot $gptDir

    $modelsDest = Join-Path $gptDir "models"
    Ensure-Dir $modelsDest
    Copy-DirectoryContents -Source (Join-Path $ReleaseAssetsDir "voice-models") -Destination $modelsDest

    # Trim VCS / caches that should not ship.
    Get-ChildItem -LiteralPath $gptDir -Recurse -Directory -Filter ".git" -ErrorAction SilentlyContinue |
        ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }
    Get-ChildItem -LiteralPath $gptDir -Recurse -Directory -Filter "__pycache__" -ErrorAction SilentlyContinue |
        ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }

    $required = @(
        "LilithVoiceHost.exe",
        "uv.exe",
        "requirements-inference.txt",
        "config\zh-cpu.yaml",
        "gpt-sovits\api_v2.py",
        "gpt-sovits\models\lilith-e15.ckpt",
        "gpt-sovits\models\lilith_e8_s288.pth",
        "gpt-sovits\GPT_SoVITS\pretrained_models\chinese-hubert-base\pytorch_model.bin"
    )
    foreach ($rel in $required) {
        if (-not (Test-Path (Join-Path $runtime $rel))) {
            throw "voice-runtime staging missing required path: $rel"
        }
    }
    Assert-RealBlob (Join-Path $runtime "gpt-sovits\models\lilith-e15.ckpt") "staged lilith model"
    Assert-RealBlob (Join-Path $runtime "gpt-sovits\GPT_SoVITS\pretrained_models\chinese-hubert-base\pytorch_model.bin") "staged hubert model"

    Ensure-Dir $PackagesDir
    New-ZipFromDirectory -SourceDir $stage -ZipPath (Join-Path $PackagesDir "voice-runtime.zip")
}

# --- main ---
Ensure-Dir $PackagesDir
Ensure-Dir $CacheDir
Ensure-Dir $StagingDir
Resolve-ReleaseAssets

if (-not $SkipCore) { Pack-Core }
if (-not $SkipVoicePack) { Pack-VoicePack }
if (-not $SkipVoiceRuntime) { Pack-VoiceRuntime }

Write-Step "Done"
Get-ChildItem -LiteralPath $PackagesDir -Filter "*.zip" | ForEach-Object {
    "{0,-20} {1,10:N1} MB" -f $_.Name, ($_.Length / 1MB)
}
