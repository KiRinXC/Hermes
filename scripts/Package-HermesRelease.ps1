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
    $source = Join-Path $feedDir $assetName
    if (Test-Path $source) {
        $destination = Join-Path $releaseDir $assetName
        Copy-Item -LiteralPath $source -Destination $destination -Force
        $copied += Get-Item -LiteralPath $destination
    }
}

if (-not ($copied | Where-Object Name -EQ "Hermes-win-Setup.exe")) {
    throw "Velopack did not create Hermes-win-Setup.exe."
}

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

Write-Host "Velopack release created:"
Write-Host "  $releaseDir"
Write-Host "Manual-test publish remains available at:"
Write-Host "  $publishDir"
