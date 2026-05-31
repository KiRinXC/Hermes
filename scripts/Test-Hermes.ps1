param(
    [switch]$UseLocalProxy,
    [string]$ProxyUrl = "http://127.0.0.1:7890"
)

$ErrorActionPreference = "Stop"
& "$PSScriptRoot\Build-Hermes.ps1" -UseLocalProxy:$UseLocalProxy -ProxyUrl $ProxyUrl
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$dotnetExe = $Global:HermesDotnetExe
$testDll = Join-Path $Global:HermesBuildOutputRoot "Debug\net10.0-windows\Hermes.Tests.dll"

& $dotnetExe $testDll
exit $LASTEXITCODE
