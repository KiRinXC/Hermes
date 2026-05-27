param(
    [string]$DotnetRoot = "D:\Code\Env\dotnet",
    [switch]$UseLocalProxy,
    [string]$ProxyUrl = "http://127.0.0.1:7890"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$dotnetExe = Join-Path $DotnetRoot "dotnet.exe"
if (-not (Test-Path $dotnetExe)) {
    throw "dotnet.exe was not found at $dotnetExe"
}

$dotnetHome = Join-Path $repoRoot ".dotnet-home"
$nugetRoot = Join-Path $repoRoot ".nuget"
$nugetOffline = Join-Path $nugetRoot "offline"

$env:DOTNET_ROOT = $DotnetRoot
$env:DOTNET_CLI_HOME = $dotnetHome
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:APPDATA = Join-Path $dotnetHome "AppData\Roaming"
$env:LOCALAPPDATA = Join-Path $dotnetHome "AppData\Local"
$env:NUGET_PACKAGES = Join-Path $nugetRoot "packages"
$env:NUGET_HTTP_CACHE_PATH = Join-Path $nugetRoot "v3-cache"
$env:NUGET_PLUGINS_CACHE_PATH = Join-Path $nugetRoot "plugins-cache"
$env:NUGET_SCRATCH = Join-Path $nugetRoot "scratch"

if ($UseLocalProxy) {
    $env:HTTP_PROXY = $ProxyUrl
    $env:HTTPS_PROXY = $ProxyUrl
}

@(
    $env:APPDATA,
    $env:LOCALAPPDATA,
    $env:DOTNET_CLI_HOME,
    $env:NUGET_PACKAGES,
    $nugetOffline,
    $env:NUGET_HTTP_CACHE_PATH,
    $env:NUGET_PLUGINS_CACHE_PATH,
    $env:NUGET_SCRATCH
) | ForEach-Object {
    New-Item -ItemType Directory -Force -Path $_ | Out-Null
}

$Global:HermesRepoRoot = $repoRoot
$Global:HermesDotnetExe = $dotnetExe
$Global:HermesNuGetConfig = Join-Path $repoRoot "NuGet.Config"
$Global:HermesNuGetOffline = $nugetOffline

Write-Host "Hermes environment is ready."
Write-Host "Repo:   $repoRoot"
Write-Host "dotnet: $dotnetExe"
Write-Host "NuGet:  $env:NUGET_PACKAGES"
