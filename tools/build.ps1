param([string]$Output = (Join-Path $PSScriptRoot '..\artifacts\release'), [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location -LiteralPath $projectRoot
try {
    if (-not $SkipTests) {
        dotnet run --project tests/AceCombat8.Tests/AceCombat8.Tests.csproj -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
    }
    $localFeed = Join-Path $projectRoot 'artifacts\empty-feed'
    New-Item -ItemType Directory -Path $localFeed -Force | Out-Null
    # The SDK-supported relative runtime lookup makes this folder portable without online runtime-pack downloads.
    dotnet publish src/AceCombat8.Desktop/AceCombat8.Desktop.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -p:AppHostRelativeDotNet=runtime -p:DebugType=None -p:DebugSymbols=false -p:NuGetAudit=false "-p:RestoreSources=$localFeed" -o $Output --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    $runtimeSource = Split-Path (Get-Command dotnet).Source
    $version = Get-ChildItem -LiteralPath (Join-Path $runtimeSource 'shared\Microsoft.WindowsDesktop.App') -Directory |
        Where-Object { $_.Name -like '9.*' -and (Test-Path -LiteralPath (Join-Path $runtimeSource ('shared\Microsoft.NETCore.App\' + $_.Name))) } |
        Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1 -ExpandProperty Name
    if (-not $version) { throw 'An installed .NET 9 Windows Desktop runtime is required to create the offline portable package.' }
    $privateRuntime = Join-Path ([IO.Path]::GetFullPath($Output)) 'runtime'
    New-Item -ItemType Directory -Path (Join-Path $privateRuntime 'host\fxr'),(Join-Path $privateRuntime 'shared\Microsoft.NETCore.App'),(Join-Path $privateRuntime 'shared\Microsoft.WindowsDesktop.App') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $runtimeSource ('host\fxr\' + $version)) -Destination (Join-Path $privateRuntime 'host\fxr') -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $runtimeSource ('shared\Microsoft.NETCore.App\' + $version)) -Destination (Join-Path $privateRuntime 'shared\Microsoft.NETCore.App') -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $runtimeSource ('shared\Microsoft.WindowsDesktop.App\' + $version)) -Destination (Join-Path $privateRuntime 'shared\Microsoft.WindowsDesktop.App') -Recurse -Force
    foreach ($name in @('dotnet.exe','LICENSE.txt','ThirdPartyNotices.txt','THIRD-PARTY-NOTICES.txt')) {
        $sourceFile=Join-Path $runtimeSource $name
        if (Test-Path -LiteralPath $sourceFile) { Copy-Item -LiteralPath $sourceFile -Destination $privateRuntime -Force }
    }
    $fixture = Join-Path $projectRoot 'tests\Fixtures\FlightStick.ini'
    $qa = Join-Path $projectRoot ('artifacts\release-qa-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    $binary = Join-Path ([IO.Path]::GetFullPath($Output)) 'AceCombat8Keybinds.exe'
    $arguments = '--smoke-test "{0}" "{1}"' -f $fixture, $qa
    $env:COREHOST_TRACE='1'
    $env:COREHOST_TRACEFILE=Join-Path $projectRoot 'artifacts\private-runtime-trace.txt'
    try { $process = Start-Process -FilePath $binary -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru }
    finally { Remove-Item Env:COREHOST_TRACE,Env:COREHOST_TRACEFILE }
    if ($process.ExitCode -ne 0) { throw "Published UI smoke test failed; inspect $qa" }
    Get-Content -LiteralPath (Join-Path $qa 'ui-results.txt')
    $trace=Get-Content -LiteralPath (Join-Path $projectRoot 'artifacts\private-runtime-trace.txt') -Raw
    if (-not $trace.Contains($privateRuntime)) { throw 'Published app did not load its private runtime.' }
    Get-FileHash -LiteralPath $binary -Algorithm SHA256
} finally { Pop-Location }
