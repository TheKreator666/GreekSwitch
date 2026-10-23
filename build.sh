#!/usr/bin/env bash
# Restore, build, test and publish GreekSwitch on any OS (the publish cross-targets win-x64).
# Stops at the first failure, including any failing test.
#   ./build.sh                 full pipeline -> publish/GreekSwitch.exe
#   ./build.sh --skip-publish  restore, build and test only
set -euo pipefail
cd "$(dirname "$0")"

CONFIGURATION="${CONFIGURATION:-Release}"
SKIP_PUBLISH=false
[[ "${1:-}" == "--skip-publish" ]] && SKIP_PUBLISH=true

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

step() { echo "==> $1"; }

step restore
dotnet restore GreekSwitch.sln

step build
dotnet build GreekSwitch.sln -c "$CONFIGURATION" --no-restore

step test
dotnet test GreekSwitch.sln -c "$CONFIGURATION" --no-build

if [[ "$SKIP_PUBLISH" == false ]]; then
  step publish
  dotnet publish src/GreekSwitch.App -c "$CONFIGURATION" -r win-x64 --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true -o publish

  # Code signing placeholder: sign publish/GreekSwitch.exe here (e.g. with osslsigncode on Linux
  # or signtool on Windows) once a certificate is available.

  echo "==> done: publish/GreekSwitch.exe"
fi
