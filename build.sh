#!/usr/bin/env bash
# Restore, build, test and publish GreekSwitch on any OS (the publish cross-targets win-x64 and win-arm64).
# Stops at the first failure, including any failing test.
#   ./build.sh                 full pipeline -> publish/win-x64/GreekSwitch.exe and publish/win-arm64/GreekSwitch.exe
#   ./build.sh --skip-publish  restore, build and test only
set -euo pipefail
cd "$(dirname "$0")"

CONFIGURATION="${CONFIGURATION:-Release}"
SKIP_PUBLISH=false
[[ "${1:-}" == "--skip-publish" ]] && SKIP_PUBLISH=true

# Each runtime gets its own self-contained single-file exe under publish/<rid>/.
RUNTIMES=(win-x64 win-arm64)

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

step() { echo "==> $1"; }

step restore
dotnet restore GreekSwitch.sln

step build
dotnet build GreekSwitch.sln -c "$CONFIGURATION" --no-restore

step test
dotnet test GreekSwitch.sln -c "$CONFIGURATION" --no-build

if [[ "$SKIP_PUBLISH" == false ]]; then
  for rid in "${RUNTIMES[@]}"; do
    out="publish/$rid"
    step "publish $rid"
    dotnet publish src/GreekSwitch.App -c "$CONFIGURATION" -r "$rid" --self-contained true \
      -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
      -p:EnableCompressionInSingleFile=true -o "$out"

    # Code signing placeholder: sign "$out/GreekSwitch.exe" here (e.g. with osslsigncode on Linux
    # or signtool on Windows) once a certificate is available. Sign before hashing.

    # The exe is unsigned: publish its SHA-256 so a copy can be checked.
    hash="$(sha256sum "$out/GreekSwitch.exe" | cut -d' ' -f1 | tr 'a-f' 'A-F')"
    printf '%s  GreekSwitch.exe\n' "$hash" > "$out/GreekSwitch.exe.sha256"
    echo "==> done: $out/GreekSwitch.exe (SHA-256 $hash)"
  done
fi
