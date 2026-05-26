param(
    [switch]$UseLocalProxy,
    [string]$ProxyUrl = "http://127.0.0.1:7890"
)

$ErrorActionPreference = "Stop"
& "$PSScriptRoot\Build-Hermes.ps1" -UseLocalProxy:$UseLocalProxy -ProxyUrl $ProxyUrl
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$dotnetExe = Join-Path "D:\Code\Env\dotnet" "dotnet.exe"
$testDll = Join-Path $repoRoot "artifacts\dotnet-verify\bin\Hermes.Tests\debug\Hermes.Tests.dll"

& $dotnetExe $testDll
exit $LASTEXITCODE
