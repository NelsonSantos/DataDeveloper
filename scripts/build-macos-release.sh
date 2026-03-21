#!/bin/zsh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_PATH="$ROOT_DIR/DataDeveloper/DataDeveloper.csproj"
APP_NAME="DataDeveloper"
DEFAULT_RUNTIME="osx-x64"
RUNTIME_IDENTIFIER="${1:-$DEFAULT_RUNTIME}"
CONFIGURATION="${CONFIGURATION:-Release}"
PUBLISH_ROOT="$ROOT_DIR/artifacts/macos/$RUNTIME_IDENTIFIER"
PUBLISH_DIR="$PUBLISH_ROOT/publish"
APP_BUNDLE="$PUBLISH_ROOT/$APP_NAME.app"
CONTENTS_DIR="$APP_BUNDLE/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
RESOURCES_DIR="$CONTENTS_DIR/Resources"
ICONSET_DIR="$PUBLISH_ROOT/AppIcon.iconset"
ICON_SOURCE="$ROOT_DIR/DataDeveloper/Assets/Icons/AppIcon.png"
PLIST_TEMPLATE="$ROOT_DIR/packaging/macos/Info.plist.template"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet nao encontrado."
  exit 1
fi

if ! command -v sips >/dev/null 2>&1; then
  echo "sips nao encontrado."
  exit 1
fi

if ! command -v iconutil >/dev/null 2>&1; then
  echo "iconutil nao encontrado."
  exit 1
fi

mkdir -p "$PUBLISH_ROOT"
rm -rf "$PUBLISH_DIR" "$APP_BUNDLE" "$ICONSET_DIR"

VERSION="$(dotnet msbuild "$PROJECT_PATH" -nologo -getProperty:Version | tail -n 1 | tr -d '\r')"
ASSEMBLY_NAME="$(dotnet msbuild "$PROJECT_PATH" -nologo -getProperty:AssemblyName | tail -n 1 | tr -d '\r')"

dotnet publish "$PROJECT_PATH" \
  -c "$CONFIGURATION" \
  -r "$RUNTIME_IDENTIFIER" \
  --self-contained true \
  -p:RestoreIgnoreFailedSources=true \
  -p:PublishSingleFile=false \
  -p:PublishTrimmed=false \
  -p:UseAppHost=true \
  -o "$PUBLISH_DIR"

mkdir -p "$MACOS_DIR" "$RESOURCES_DIR" "$ICONSET_DIR"
cp -R "$PUBLISH_DIR"/. "$MACOS_DIR/"

create_icon() {
  local size="$1"
  local scale="$2"
  local file="$ICONSET_DIR/icon_${size}x${size}${scale}.png"
  local pixels="$size"
  if [[ "$scale" == "@2x" ]]; then
    pixels=$((size * 2))
  fi
  sips -z "$pixels" "$pixels" "$ICON_SOURCE" --out "$file" >/dev/null
}

for size in 16 32 128 256 512; do
  create_icon "$size" ""
  create_icon "$size" "@2x"
done

iconutil -c icns "$ICONSET_DIR" -o "$RESOURCES_DIR/AppIcon.icns"

sed \
  -e "s/__APP_NAME__/$APP_NAME/g" \
  -e "s/__ASSEMBLY_NAME__/$ASSEMBLY_NAME/g" \
  -e "s/__VERSION__/$VERSION/g" \
  -e "s/__RUNTIME_IDENTIFIER__/$RUNTIME_IDENTIFIER/g" \
  "$PLIST_TEMPLATE" > "$CONTENTS_DIR/Info.plist"

printf "APPL????" > "$CONTENTS_DIR/PkgInfo"
chmod +x "$MACOS_DIR/$ASSEMBLY_NAME"
xattr -cr "$APP_BUNDLE" 2>/dev/null || true

if command -v codesign >/dev/null 2>&1; then
  codesign --force --deep --sign - "$APP_BUNDLE" >/dev/null 2>&1 || true
fi

ditto -c -k --sequesterRsrc --keepParent "$APP_BUNDLE" "$PUBLISH_ROOT/$APP_NAME-$RUNTIME_IDENTIFIER.zip"

echo "Release gerada em:"
echo "  $APP_BUNDLE"
echo "Zip gerado em:"
echo "  $PUBLISH_ROOT/$APP_NAME-$RUNTIME_IDENTIFIER.zip"
