param(
    [switch]$UseLocalProxy,
    [string]$ProxyUrl = "http://127.0.0.1:7890"
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\Use-HermesEnv.ps1" -UseLocalProxy:$UseLocalProxy -ProxyUrl $ProxyUrl

$output = Join-Path $Global:HermesRepoRoot "artifacts\publish\Hermes.Windows\manual-test\win-x64-self-contained"
$outputExe = Join-Path $output "Hermes.Windows.exe"
$lockingProcesses = Get-Process -Name "Hermes.Windows" -ErrorAction SilentlyContinue | Where-Object {
    try {
        [string]::Equals($_.Path, $outputExe, [System.StringComparison]::OrdinalIgnoreCase)
    }
    catch {
        $false
    }
}
if ($lockingProcesses) {
    $processIds = ($lockingProcesses | ForEach-Object Id) -join ", "
    throw "Hermes is running from the publish directory (PID: $processIds). Exit it from the system tray before publishing."
}

& $Global:HermesDotnetExe publish "$Global:HermesRepoRoot\src\Hermes.Windows\Hermes.Windows.csproj" `
    --configfile "$Global:HermesNuGetConfig" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:NuGetAudit=false `
    -p:MSBuildEnableWorkloadResolver=false `
    -p:HermesIntermediateRoot="$Global:HermesBuildIntermediateRoot" `
    --artifacts-path "$Global:HermesRepoRoot\artifacts\dotnet" `
    -o "$output" `
    --tl:off

exit $LASTEXITCODE
