#!/usr/bin/env pwsh
# Restore, build, test and publish GreekSwitch. Stops at the first failure (including any failing test).
#   ./build.ps1               full pipeline -> publish/GreekSwitch.exe
#   ./build.ps1 -SkipPublish  restore, build and test only
param(
    [string]$Configuration = "Release",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

function Invoke-Step([string]$Name, [scriptblock]$Command) {
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $Name (exit code $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

Invoke-Step "restore" { dotnet restore GreekSwitch.sln }
Invoke-Step "build"   { dotnet build GreekSwitch.sln -c $Configuration --no-restore }
Invoke-Step "test"    { dotnet test GreekSwitch.sln -c $Configuration --no-build }

if (-not $SkipPublish) {
    Invoke-Step "publish" {
        dotnet publish src/GreekSwitch.App -c $Configuration -r win-x64 --self-contained true `
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:EnableCompressionInSingleFile=true -o publish
    }

    # Code signing placeholder: sign publish/GreekSwitch.exe here once a certificate is available, e.g.
    #   signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a publish/GreekSwitch.exe

    # The exe is unsigned: publish its SHA-256 so a copy can be checked with Get-FileHash.
    $hash = (Get-FileHash publish/GreekSwitch.exe -Algorithm SHA256).Hash
    Set-Content -Path publish/GreekSwitch.exe.sha256 -Value "$hash  GreekSwitch.exe" -Encoding ascii
    Write-Host "==> done: publish/GreekSwitch.exe (SHA-256 $hash)" -ForegroundColor Green
}
