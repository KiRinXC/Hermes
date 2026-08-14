param(
    [string]$Version = "0.4.0",
    [switch]$SkipPublish,
    [switch]$UseLocalProxy,
    [string]$ProxyUrl = "http://127.0.0.1:7890"
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\Use-HermesEnv.ps1" -UseLocalProxy:$UseLocalProxy -ProxyUrl $ProxyUrl

$normalizedVersion = $Version -replace '^[vV]', ''
$tag = "v$normalizedVersion"
$publishDir = Join-Path $Global:HermesRepoRoot "artifacts\publish\Hermes.Windows\manual-test\win-x64-self-contained"
$feedDir = Join-Path $Global:HermesRepoRoot "artifacts\release\velopack-feed\win"
$releaseDir = Join-Path $Global:HermesRepoRoot "artifacts\release\$tag"
$releaseNotes = Join-Path $Global:HermesRepoRoot "docs\release-notes\$tag.md"
$iconPath = Join-Path $Global:HermesRepoRoot "src\Hermes.Windows\Resources\AppIcon.ico"
$checksumPath = Join-Path $releaseDir "checksums.txt"
$bootstrapSourceDir = Join-Path $Global:HermesRepoRoot "src\Hermes.SetupBootstrap"
$bootstrapBuildDir = Join-Path $Global:HermesRepoRoot "artifacts\release\setup-bootstrap\$tag"

function Resolve-HermesNativeTool {
    param([Parameter(Mandatory)][string]$Name)

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    foreach ($toolRoot in @(
        "C:\Code\Env\C\w64devkit\bin",
        "D:\Code\Env\C\w64devkit\bin"
    )) {
        $candidate = Join-Path $toolRoot "$Name.exe"
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "Required native packaging tool was not found: $Name. Install w64devkit or add it to PATH."
}

$publishExe = Join-Path $publishDir "Hermes.Windows.exe"
$lockingProcesses = Get-Process Hermes.Windows -ErrorAction SilentlyContinue | Where-Object {
    try {
        [string]::Equals($_.Path, $publishExe, [System.StringComparison]::OrdinalIgnoreCase)
    }
    catch {
        $false
    }
}
if ($lockingProcesses) {
    $processIds = ($lockingProcesses | ForEach-Object { $_.Id }) -join ", "
    throw "Hermes is running from the publish directory. Exit it before packaging. Process id(s): $processIds"
}

if (-not $SkipPublish) {
    & "$PSScriptRoot\Publish-Hermes.ps1" -UseLocalProxy:$UseLocalProxy -ProxyUrl $ProxyUrl
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if (-not (Test-Path $publishDir)) {
    throw "Publish directory does not exist: $publishDir"
}

if (-not (Test-Path $releaseNotes)) {
    throw "Release notes do not exist: $releaseNotes"
}

New-Item -ItemType Directory -Force -Path $feedDir | Out-Null
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

# Rebuilding the same version is a normal local verification workflow. Remove only
# the exact generated assets for this version and feed indexes; older nupkg files stay
# in place so Velopack can still create deltas for a newer version.
$generatedAssets = @(
    "Hermes-$normalizedVersion-full.nupkg",
    "Hermes-$normalizedVersion-delta.nupkg",
    "Hermes-win-Portable.zip",
    "Hermes-win-Setup.exe",
    "releases.win.json",
    "assets.win.json",
    "RELEASES"
)
foreach ($assetName in $generatedAssets) {
    $assetPath = Join-Path $feedDir $assetName
    if (Test-Path $assetPath) {
        Remove-Item -LiteralPath $assetPath -Force
    }
}

Push-Location $Global:HermesRepoRoot
try {
    & $Global:HermesDotnetExe tool restore
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    & $Global:HermesDotnetExe vpk pack `
        --packId Hermes `
        --packVersion $normalizedVersion `
        --packDir $publishDir `
        --mainExe "Hermes.Windows.exe" `
        --packTitle "Hermes" `
        --packAuthors "KiRinXC" `
        --runtime "win-x64" `
        --channel "win" `
        --icon $iconPath `
        --releaseNotes $releaseNotes `
        --outputDir $feedDir
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}

$releaseAssetNames = @(
    "Hermes-win-Setup.exe",
    "Hermes-$normalizedVersion-full.nupkg",
    "Hermes-$normalizedVersion-delta.nupkg",
    "releases.win.json"
)

$staleReleaseAssetNames = @($generatedAssets + "checksums.txt") | Select-Object -Unique
foreach ($assetName in $staleReleaseAssetNames) {
    $assetPath = Join-Path $releaseDir $assetName
    if (Test-Path $assetPath) {
        Remove-Item -LiteralPath $assetPath -Force
    }
}

$copied = @()
foreach ($assetName in $releaseAssetNames) {
    if ($assetName -eq "Hermes-win-Setup.exe") {
        continue
    }

    $source = Join-Path $feedDir $assetName
    if (Test-Path $source) {
        $destination = Join-Path $releaseDir $assetName
        Copy-Item -LiteralPath $source -Destination $destination -Force
        $copied += Get-Item -LiteralPath $destination
    }
}

$coreSetupPath = Join-Path $feedDir "Hermes-win-Setup.exe"
if (-not (Test-Path $coreSetupPath)) {
    throw "Velopack did not create the core Hermes-win-Setup.exe."
}

$gxxExe = Resolve-HermesNativeTool -Name "g++"
$windresExe = Resolve-HermesNativeTool -Name "windres"
New-Item -ItemType Directory -Force -Path $bootstrapBuildDir | Out-Null

$bootstrapResource = Join-Path $bootstrapBuildDir "SetupBootstrap.rc"
$bootstrapManifest = Join-Path $bootstrapBuildDir "app.manifest"
$bootstrapObject = Join-Path $bootstrapBuildDir "SetupBootstrapResources.o"
$bootstrapSetup = Join-Path $releaseDir "Hermes-win-Setup.exe"
Copy-Item -LiteralPath (Join-Path $bootstrapSourceDir "SetupBootstrap.rc") -Destination $bootstrapResource -Force
Copy-Item -LiteralPath (Join-Path $bootstrapSourceDir "app.manifest") -Destination $bootstrapManifest -Force
Copy-Item -LiteralPath $iconPath -Destination (Join-Path $bootstrapBuildDir "Hermes.ico") -Force
Copy-Item -LiteralPath $coreSetupPath -Destination (Join-Path $bootstrapBuildDir "Hermes-Core-Setup.exe") -Force

$versionParts = @(($normalizedVersion -split '-', 2)[0] -split '\.')
while ($versionParts.Count -lt 4) {
    $versionParts += "0"
}
$manifestVersion = $versionParts[0..3] -join "."
$manifestContent = Get-Content -Raw -LiteralPath $bootstrapManifest
$manifestContent = $manifestContent.Replace('version="0.0.0.0"', "version=`"$manifestVersion`"")
$manifestContent | Set-Content -LiteralPath $bootstrapManifest -Encoding UTF8

Push-Location $bootstrapBuildDir
try {
    & $windresExe "SetupBootstrap.rc" -O coff -o $bootstrapObject
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to compile Hermes setup resources."
    }

    & $gxxExe `
        (Join-Path $bootstrapSourceDir "SetupBootstrap.cpp") `
        $bootstrapObject `
        -o $bootstrapSetup `
        -std=c++17 `
        -municode `
        -mwindows `
        -Os `
        -s `
        -static `
        -static-libgcc `
        -static-libstdc++ `
        -lshell32 `
        -lole32 `
        -luuid
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build the Hermes setup bootstrap."
    }
}
finally {
    Pop-Location
}

$verificationProcess = Start-Process -FilePath $bootstrapSetup -ArgumentList "--verify-package" -Wait -PassThru
if ($verificationProcess.ExitCode -ne 0) {
    throw "The public setup bootstrap does not contain a valid Velopack setup package."
}

$migrationProbe = Join-Path $bootstrapBuildDir "migration-probe"
if (Test-Path $migrationProbe) {
    Remove-Item -LiteralPath $migrationProbe -Recurse -Force
}
$probeSource = Join-Path $migrationProbe "source"
$probeTarget = Join-Path $migrationProbe "target"
$probeAppSource = Join-Path $probeSource "apps\codex-auth-switch-sync"
New-Item -ItemType Directory -Force -Path $probeAppSource | Out-Null
"settings-probe" | Set-Content -LiteralPath (Join-Path $probeSource "settings.json") -Encoding UTF8
"profile-probe" | Set-Content -LiteralPath (Join-Path $probeAppSource "profiles.json") -Encoding UTF8
$migrationProcess = Start-Process `
    -FilePath $bootstrapSetup `
    -ArgumentList "--test-migrate `"$probeSource`" `"$probeTarget`"" `
    -Wait `
    -PassThru
if (($migrationProcess.ExitCode -ne 0) `
    -or -not (Test-Path (Join-Path $probeTarget "settings.json")) `
    -or -not (Test-Path (Join-Path $probeTarget "apps\codex-auth-switch-sync\profiles.json"))) {
    throw "The setup bootstrap user-data migration probe failed."
}
Remove-Item -LiteralPath $migrationProbe -Recurse -Force
$copied += Get-Item -LiteralPath $bootstrapSetup

if (-not ($copied | Where-Object Name -EQ "Hermes-$normalizedVersion-full.nupkg")) {
    throw "Velopack did not create the full update package."
}

if (-not ($copied | Where-Object Name -EQ "releases.win.json")) {
    throw "Velopack did not create releases.win.json."
}

$hashLines = foreach ($asset in $copied | Sort-Object Name) {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $asset.FullName
    "$($hash.Hash.ToLowerInvariant())  $($asset.Name)"
}
$hashLines | Set-Content -LiteralPath $checksumPath -Encoding UTF8

Write-Host "Hermes installer and Velopack update release created:"
Write-Host "  $releaseDir"
Write-Host "Manual-test publish remains available at:"
Write-Host "  $publishDir"
