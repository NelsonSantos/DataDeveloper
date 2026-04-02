#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_PATH="$ROOT_DIR/DataDeveloper/DataDeveloper.csproj"
APP_NAME="DataDeveloper"
DEFAULT_RUNTIME="linux-x64"
RUNTIME_IDENTIFIER="${1:-$DEFAULT_RUNTIME}"
CONFIGURATION="${CONFIGURATION:-Release}"
VERSION_OVERRIDE="${VERSION:-}"
VERSION_OVERRIDE="${VERSION_OVERRIDE#v}"
VERSION_OVERRIDE="${VERSION_OVERRIDE#V}"
PUBLISH_ROOT="$ROOT_DIR/artifacts/linux/$RUNTIME_IDENTIFIER"
PUBLISH_DIR="$PUBLISH_ROOT/publish"
APPDIR="$PUBLISH_ROOT/$APP_NAME.AppDir"
PACKAGE_DIR="$ROOT_DIR/packaging/linux"
ICON_SOURCE="$ROOT_DIR/DataDeveloper/Assets/Icons/AppIcon.png"
NUGET_SOURCE="${NUGET_SOURCE:-https://api.nuget.org/v3/index.json}"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet not found."
  exit 1
fi

mkdir -p "$PUBLISH_ROOT"
rm -rf "$PUBLISH_DIR" "$APPDIR"

VERSION_MSBUILD_ARGS=()
if [[ -n "$VERSION_OVERRIDE" ]]; then
  VERSION_MSBUILD_ARGS+=("-p:Version=$VERSION_OVERRIDE" "-p:VersionPrefix=$VERSION_OVERRIDE")
fi

VERSION="$(dotnet msbuild "$PROJECT_PATH" -nologo "${VERSION_MSBUILD_ARGS[@]}" -getProperty:Version | tail -n 1 | tr -d '\r')"
ASSEMBLY_NAME="$(dotnet msbuild "$PROJECT_PATH" -nologo -getProperty:AssemblyName | tail -n 1 | tr -d '\r')"

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

mkdir -p "$APPDIR/usr/bin"
cp -R "$PUBLISH_DIR"/. "$APPDIR/usr/bin/"
cp "$PACKAGE_DIR/AppRun" "$APPDIR/AppRun"
cp "$PACKAGE_DIR/$APP_NAME.desktop" "$APPDIR/$APP_NAME.desktop"
cp "$ICON_SOURCE" "$APPDIR/$APP_NAME.png"
chmod +x "$APPDIR/AppRun"

sed -i.bak \
  -e "s/__APP_NAME__/$APP_NAME/g" \
  -e "s/__ASSEMBLY_NAME__/$ASSEMBLY_NAME/g" \
  -e "s/__VERSION__/$VERSION/g" \
  "$APPDIR/$APP_NAME.desktop"
rm -f "$APPDIR/$APP_NAME.desktop.bak"

echo "Linux publish generated at:"
echo "  $PUBLISH_DIR"
echo
echo "AppDir generated at:"
echo "  $APPDIR"
echo
echo "To build the AppImage on Linux:"
echo "  $PACKAGE_DIR/build-appimage.sh $APPDIR $RUNTIME_IDENTIFIER"
