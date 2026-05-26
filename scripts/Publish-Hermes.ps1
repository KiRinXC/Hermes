param(
    [switch]$UseLocalProxy,
    [string]$ProxyUrl = "http://127.0.0.1:7890"
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\Use-HermesEnv.ps1" -UseLocalProxy:$UseLocalProxy -ProxyUrl $ProxyUrl

$output = Join-Path $Global:HermesRepoRoot "artifacts\publish\Hermes.Windows\manual-test\win-x64-self-contained"

& $Global:HermesDotnetExe publish "$Global:HermesRepoRoot\src\Hermes.Windows\Hermes.Windows.csproj" `
    --configfile "$Global:HermesNuGetConfig" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:NuGetAudit=false `
    --artifacts-path "$Global:HermesRepoRoot\artifacts\dotnet" `
    -o "$output" `
    --tl:off

exit $LASTEXITCODE
