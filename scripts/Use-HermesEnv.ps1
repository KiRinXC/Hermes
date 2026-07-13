param(
    [string]$DotnetRoot = $env:HERMES_DOTNET_ROOT,
    [switch]$UseLocalProxy,
    [string]$ProxyUrl = "http://127.0.0.1:7890"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($DotnetRoot)) {
    $dotnetCandidates = @(
        "C:\Code\Env\dotnet",
        "D:\Code\Env\dotnet"
    )

    $dotnetCommand = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($null -ne $dotnetCommand) {
        $dotnetCandidates += Split-Path -Parent $dotnetCommand.Source
    }

    $DotnetRoot = $dotnetCandidates |
        Where-Object { Test-Path (Join-Path $_ "dotnet.exe") } |
        Select-Object -First 1
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($DotnetRoot)) {
    throw "dotnet.exe was not found. Set HERMES_DOTNET_ROOT or install the configured workspace SDK."
}

$dotnetExe = Join-Path $DotnetRoot "dotnet.exe"
if (-not (Test-Path $dotnetExe)) {
    throw "dotnet.exe was not found at $dotnetExe"
}

$runtimeRoot = Join-Path $env:TEMP "HermesRuntime"
$dotnetHome = Join-Path $runtimeRoot ".dotnet-home"
$nugetRoot = Join-Path $runtimeRoot ".nuget"
$nugetOffline = Join-Path $repoRoot ".nuget\offline"
$buildRoot = Join-Path $runtimeRoot "build"
$buildIntermediate = Join-Path $buildRoot "obj"
$buildRunId = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$buildOutput = Join-Path $repoRoot "artifacts\dotnet-verify\bin\$buildRunId"

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
    $runtimeRoot,
    $env:APPDATA,
    $env:LOCALAPPDATA,
    $env:DOTNET_CLI_HOME,
    $env:NUGET_PACKAGES,
    $nugetOffline,
    $env:NUGET_HTTP_CACHE_PATH,
    $env:NUGET_PLUGINS_CACHE_PATH,
    $env:NUGET_SCRATCH,
    $buildRoot,
    $buildIntermediate,
    $buildOutput
) | ForEach-Object {
    New-Item -ItemType Directory -Force -Path $_ | Out-Null
}

$Global:HermesRepoRoot = $repoRoot
$Global:HermesDotnetExe = $dotnetExe
$Global:HermesNuGetConfig = Join-Path $repoRoot "NuGet.Config"
$Global:HermesNuGetOffline = $nugetOffline
$Global:HermesBuildIntermediateRoot = $buildIntermediate
$Global:HermesBuildOutputRoot = $buildOutput

Write-Host "Hermes environment is ready."
Write-Host "Repo:   $repoRoot"
Write-Host "dotnet: $dotnetExe"
Write-Host "NuGet:  $env:NUGET_PACKAGES"
