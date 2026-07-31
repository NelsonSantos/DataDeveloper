#!/bin/sh

set -eu

if [ "$#" -lt 2 ]; then
  echo "Usage: $0 <AppDir> <runtime-identifier> [version]"
  exit 1
fi

APPDIR="$1"
RUNTIME_IDENTIFIER="$2"
VERSION="${3:-}"
ROOT_DIR="$(CDPATH= cd -- "$(dirname "$0")/../.." && pwd)"
OUTPUT_DIR="$ROOT_DIR/artifacts/linux/$RUNTIME_IDENTIFIER"
APPIMAGE_TOOL="${APPIMAGE_TOOL:-appimagetool}"
if [ -n "$VERSION" ]; then
  OUTPUT_FILE="$OUTPUT_DIR/DataDeveloper-$VERSION-$RUNTIME_IDENTIFIER.AppImage"
else
  OUTPUT_FILE="$OUTPUT_DIR/DataDeveloper-$RUNTIME_IDENTIFIER.AppImage"
fi

if ! command -v "$APPIMAGE_TOOL" >/dev/null 2>&1; then
  echo "$APPIMAGE_TOOL not found."
  exit 1
fi

mkdir -p "$OUTPUT_DIR"
"$APPIMAGE_TOOL" "$APPDIR" "$OUTPUT_FILE"

echo "AppImage generated at:"
echo "  $OUTPUT_FILE"
