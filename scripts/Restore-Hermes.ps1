param(
    [switch]$UseLocalProxy,
    [string]$ProxyUrl = "http://127.0.0.1:7890"
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\Use-HermesEnv.ps1" -UseLocalProxy:$UseLocalProxy -ProxyUrl $ProxyUrl

& $Global:HermesDotnetExe restore "$Global:HermesRepoRoot\src\Hermes.Windows\Hermes.Windows.csproj" `
    --configfile "$Global:HermesNuGetConfig" `
    -r win-x64 `
    -p:SelfContained=true `
    -p:NuGetAudit=false `
    -p:MSBuildEnableWorkloadResolver=false `
    -p:HermesIntermediateRoot="$Global:HermesBuildIntermediateRoot" `
    --artifacts-path "$Global:HermesRepoRoot\artifacts\dotnet" `
    -v normal `
    --tl:off

exit $LASTEXITCODE
