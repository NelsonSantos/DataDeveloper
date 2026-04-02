#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_PATH="$ROOT_DIR/DataDeveloper/DataDeveloper.csproj"
APP_NAME="DataDeveloper"
DEFAULT_RUNTIME="win-x64"
RUNTIME_IDENTIFIER="${1:-$DEFAULT_RUNTIME}"
CONFIGURATION="${CONFIGURATION:-Release}"
VERSION_OVERRIDE="${VERSION:-}"
VERSION_OVERRIDE="${VERSION_OVERRIDE#v}"
VERSION_OVERRIDE="${VERSION_OVERRIDE#V}"
PUBLISH_ROOT="$ROOT_DIR/artifacts/windows/$RUNTIME_IDENTIFIER"
PUBLISH_DIR="$PUBLISH_ROOT/publish"
PACKAGE_DIR="$ROOT_DIR/packaging/windows"
ISS_FILE="$PACKAGE_DIR/$APP_NAME.iss"
NUGET_SOURCE="${NUGET_SOURCE:-https://api.nuget.org/v3/index.json}"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet not found."
  exit 1
fi

mkdir -p "$PUBLISH_ROOT"
rm -rf "$PUBLISH_DIR"

VERSION="$(dotnet msbuild "$PROJECT_PATH" -nologo -getProperty:Version | tail -n 1 | tr -d '\r')"
VERSION_MSBUILD_ARGS=()
if [[ -n "$VERSION_OVERRIDE" ]]; then
  VERSION_MSBUILD_ARGS+=("-p:Version=$VERSION_OVERRIDE" "-p:VersionPrefix=$VERSION_OVERRIDE")
  VERSION="$VERSION_OVERRIDE"
fi

dotnet restore "$PROJECT_PATH" \
  -r "$RUNTIME_IDENTIFIER" \
  -p:RestoreIgnoreFailedSources=true \
  "${VERSION_MSBUILD_ARGS[@]}" \
  --source "$NUGET_SOURCE"

dotnet publish "$PROJECT_PATH" \
  -c "$CONFIGURATION" \
  -r "$RUNTIME_IDENTIFIER" \
  --self-contained true \
  --no-restore \
  --source "$NUGET_SOURCE" \
  -p:RestoreIgnoreFailedSources=true \
  "${VERSION_MSBUILD_ARGS[@]}" \
  -p:PublishSingleFile=false \
  -p:PublishTrimmed=false \
  -p:UseAppHost=true \
  -o "$PUBLISH_DIR"

echo "Windows publish generated at:"
echo "  $PUBLISH_DIR"
echo
echo "Version:"
echo "  $VERSION"
echo
echo "To build the installer on Windows with Inno Setup:"
echo "  ISCC $ISS_FILE /DAppVersion=$VERSION /DPublishDir=$PUBLISH_DIR /DPlatform=$RUNTIME_IDENTIFIER"
