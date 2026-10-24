#!/usr/bin/env pwsh
# Restore, build, test and publish GreekSwitch. Stops at the first failure (including any failing test).
#   ./build.ps1               full pipeline -> publish/win-x64/GreekSwitch.exe and publish/win-arm64/GreekSwitch.exe
#   ./build.ps1 -SkipPublish  restore, build and test only
param(
    [string]$Configuration = "Release",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

# Each runtime gets its own self-contained single-file exe under publish/<rid>/.
$Runtimes = @("win-x64", "win-arm64")

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
    foreach ($rid in $Runtimes) {
        $out = "publish/$rid"
        Invoke-Step "publish $rid" {
            dotnet publish src/GreekSwitch.App -c $Configuration -r $rid --self-contained true `
                -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
                -p:EnableCompressionInSingleFile=true -o $out
        }

        # Code signing placeholder: sign each exe here once a certificate is available, e.g.
        #   signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a publish/<rid>/GreekSwitch.exe
        # Sign before hashing, so the .sha256 matches the signed file.

        # The exe is unsigned: publish its SHA-256 so a copy can be checked with Get-FileHash.
        $hash = (Get-FileHash "$out/GreekSwitch.exe" -Algorithm SHA256).Hash
        Set-Content -Path "$out/GreekSwitch.exe.sha256" -Value "$hash  GreekSwitch.exe" -Encoding ascii
        Write-Host "==> done: $out/GreekSwitch.exe (SHA-256 $hash)" -ForegroundColor Green
    }
}
