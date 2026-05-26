param(
    [switch]$UseLocalProxy,
    [string]$ProxyUrl = "http://127.0.0.1:7890"
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\Use-HermesEnv.ps1" -UseLocalProxy:$UseLocalProxy -ProxyUrl $ProxyUrl

& $Global:HermesDotnetExe build "$Global:HermesRepoRoot\Hermes.sln" `
    --configfile "$Global:HermesNuGetConfig" `
    -p:NuGetAudit=false `
    --artifacts-path "$Global:HermesRepoRoot\artifacts\dotnet-verify" `
    --tl:off

exit $LASTEXITCODE
