param(
    [string]$Version = "0.3.0",
    [switch]$SkipPublish,
    [switch]$UseLocalProxy,
    [string]$ProxyUrl = "http://127.0.0.1:7890"
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\Use-HermesEnv.ps1" -UseLocalProxy:$UseLocalProxy -ProxyUrl $ProxyUrl

$normalizedVersion = $Version -replace '^[vV]', ''
$tag = "v$normalizedVersion"
$publishDir = Join-Path $Global:HermesRepoRoot "artifacts\publish\Hermes.Windows\manual-test\win-x64-self-contained"
$releaseDir = Join-Path $Global:HermesRepoRoot "artifacts\release\$tag"
$zipName = "Hermes-$tag-win-x64-portable.zip"
$zipPath = Join-Path $releaseDir $zipName
$checksumPath = Join-Path $releaseDir "checksums.txt"

$running = Get-Process Hermes.Windows -ErrorAction SilentlyContinue
if ($running) {
    $ids = ($running | ForEach-Object { $_.Id }) -join ", "
    throw "Hermes.Windows is running and may lock release files. Close it first. Process id(s): $ids"
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

New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath
"$($hash.Hash.ToLowerInvariant())  $zipName" | Set-Content -Path $checksumPath -Encoding UTF8

Write-Host "Release package created:"
Write-Host "  $zipPath"
Write-Host "Checksum:"
Write-Host "  $checksumPath"
