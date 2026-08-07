param([Parameter(Mandatory = $true)][string]$Configuration)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$toolingRoot = [Environment]::GetEnvironmentVariable('RWT_CASCADE_TOOLING_ROOT')
$outputRoot = [Environment]::GetEnvironmentVariable('RWT_CASCADE_BUILD_OUTPUT_ROOT')
if ([string]::IsNullOrWhiteSpace($toolingRoot) -or [string]::IsNullOrWhiteSpace($outputRoot)) { throw 'Cascade build environment is missing.' }
$buildScript = Join-Path ([System.IO.Path]::GetFullPath($toolingRoot)) 'tools\Invoke-RimWorldBuild.ps1'
$resultPath = Join-Path $outputRoot 'build-result.json'
& powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $buildScript `
    -Project (Join-Path $repository 'Source\Mod.csproj') -Configuration $Configuration -Version $Configuration `
    -OutputRoot $outputRoot -Engine MSBuild -Dependency @('harmony', 'spine') -ResultPath $resultPath | Out-Null
if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) { throw "No build result was returned for $Configuration." }
$result = Get-Content -Raw -LiteralPath $resultPath | ConvertFrom-Json
if (-not [bool]$result.Succeeded) { throw "RimWorld $Configuration build failed with exit code $($result.ExitCode)." }
$built = Join-Path $outputRoot 'build\CaravanReadiness.dll'
if (-not (Test-Path -LiteralPath $built -PathType Leaf)) { throw 'CaravanReadiness.dll was not produced.' }
$payload = Join-Path $repository "$Configuration\Assemblies"
[System.IO.Directory]::CreateDirectory($payload) | Out-Null
[System.IO.File]::Copy($built, (Join-Path $payload 'CaravanReadiness.dll'), $true)
