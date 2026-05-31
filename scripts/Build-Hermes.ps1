param(
    [switch]$UseLocalProxy,
    [string]$ProxyUrl = "http://127.0.0.1:7890"
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\Use-HermesEnv.ps1" -UseLocalProxy:$UseLocalProxy -ProxyUrl $ProxyUrl

& $Global:HermesDotnetExe build "$Global:HermesRepoRoot\tests\Hermes.Tests\Hermes.Tests.csproj" `
    --configfile "$Global:HermesNuGetConfig" `
    -p:NuGetAudit=false `
    -p:MSBuildEnableWorkloadResolver=false `
    -p:BaseIntermediateOutputPath="$Global:HermesBuildIntermediateRoot\" `
    -p:BaseOutputPath="$Global:HermesBuildOutputRoot\" `
    --tl:off

exit $LASTEXITCODE
