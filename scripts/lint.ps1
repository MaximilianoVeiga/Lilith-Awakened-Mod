param(
    [switch]$AllowSkipInjector
)

$ErrorActionPreference = "Stop"

$sdks = @(dotnet --list-sdks)
if ($sdks.Count -eq 0) {
    throw "A .NET SDK is required. Only the .NET runtime is currently installed."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$projects = @(
    "src/LilithTextInjector/LilithTextInjector.csproj",
    "src/LilithModInstaller/LilithModInstaller.csproj",
    "src/LilithVoiceHost/LilithVoiceHost.csproj"
)

$gameInterop = if ($env:GameInterop) {
    $env:GameInterop
} else {
    "C:\Program Files (x86)\Steam\steamapps\common\The NOexistenceN of Lilith\BepInEx\interop"
}
$bepInExCore = Join-Path $repoRoot "src\bepinex-be780\BepInEx\core\BepInEx.Core.dll"
$canLintInjector =
    (Test-Path (Join-Path $gameInterop "Assembly-CSharp.dll")) -and
    (Test-Path $bepInExCore)

foreach ($project in $projects) {
    if ($project -like "*LilithTextInjector*" -and -not $canLintInjector) {
        if (-not $AllowSkipInjector) {
            throw @"
Cannot lint ${project}: game interop or BepInEx references not found.
Set GameInterop to the BepInEx/interop folder, ensure src/bepinex-be780 is present, or pass -AllowSkipInjector to skip this project.
"@
        }

        Write-Host "Skipping $project (game interop or BepInEx references not found; -AllowSkipInjector)."
        continue
    }

    Write-Host "Linting $project"
    dotnet restore $project
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    dotnet format analyzers $project --verify-no-changes --severity warn
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    dotnet build $project --no-restore -warnaserror
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
