param(
    [string]$WinAppPath = 'E:\DevTools\winapp\winappcli-x64\winapp.exe',
    [switch]$BuildOnly,
    [ValidateSet('system', 'en-US', 'ko-KR')]
    [string]$Language = 'system'
)

# Run after loading your local .NET environment. Windows language stays unchanged.
# Debug language previews are cleared by the app on close or its next normal launch.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$projectRoot = Split-Path -Parent $PSScriptRoot
$appDirectory = Join-Path $projectRoot 'src\FileHelper'
$logDirectory = Join-Path $projectRoot '.local\logs'

if (-not (Test-Path -LiteralPath (Join-Path $appDirectory 'FileHelper.csproj'))) {
    throw "Project not found: $appDirectory"
}
if ($BuildOnly) {
    $command = (Get-Command dotnet -ErrorAction Stop).Source
    $commandArgs = @('build', 'FileHelper.csproj', '-c', 'Debug', '-r', 'win-x64', '-v', 'minimal', '-tl:off', '-p:Platform=x64')
} else {
    if (-not (Test-Path -LiteralPath $WinAppPath -PathType Leaf)) {
        throw "winapp not found. Supply its location with -WinAppPath. Current path: $WinAppPath"
    }
    $command = $WinAppPath
    $commandArgs = @('run', '--args', "--test-language=$Language")
}

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory ("run-{0}.log" -f (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$exitCode = 1
Push-Location -LiteralPath $appDirectory
try {
    @(
        "Started: $(Get-Date -Format o)"
        "Directory: $appDirectory"
        "Command: $command $($commandArgs -join ' ')"
        ''
    ) | Tee-Object -FilePath $logPath

    & $command @commandArgs 2>&1 | Tee-Object -FilePath $logPath -Append
    $exitCode = $LASTEXITCODE
} catch {
    "Runner error: $($_.Exception.Message)" | Tee-Object -FilePath $logPath -Append
    $exitCode = 1
} finally {
    @('', "Finished: $(Get-Date -Format o)", "Exit code: $exitCode") |
        Tee-Object -FilePath $logPath -Append
    Pop-Location
    Write-Host "Log saved: $logPath"
}
exit $exitCode
