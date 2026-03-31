#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  ./scripts/create-release.sh <version> <notes-file>

Example:
  ./scripts/create-release.sh 26.0330.1 release-notes/26.0330.1.md

What it does:
  1. Creates an annotated git tag: v<version>
  2. Pushes the tag to origin
  3. Waits for the GitHub Release to be created by Actions
  4. Replaces the auto-generated release notes with the contents of <notes-file>

Requirements:
  - git
  - gh (GitHub CLI) authenticated for this repository
EOF
}

if [[ $# -ne 2 ]]; then
  usage
  exit 1
fi

VERSION="$1"
NOTES_FILE="$2"
TAG="v${VERSION}"
MAX_ATTEMPTS="${MAX_ATTEMPTS:-120}"
SLEEP_SECONDS="${SLEEP_SECONDS:-5}"

if [[ ! -f "$NOTES_FILE" ]]; then
  echo "Notes file not found: $NOTES_FILE" >&2
  exit 1
fi

if ! command -v gh >/dev/null 2>&1; then
  echo "GitHub CLI 'gh' is required." >&2
  exit 1
fi

CURRENT_BRANCH="$(git branch --show-current)"
if [[ "$CURRENT_BRANCH" != "main" ]]; then
  echo "Current branch is '$CURRENT_BRANCH'. Switch to 'main' before creating a release." >&2
  exit 1
fi

if [[ -n "$(git status --short)" ]]; then
  echo "Working tree is not clean. Commit or stash changes before creating a release." >&2
  exit 1
fi

if git rev-parse "$TAG" >/dev/null 2>&1; then
  echo "Tag already exists locally: $TAG" >&2
  exit 1
fi

echo "Creating tag $TAG"
git tag -a "$TAG" -m "$TAG"

echo "Pushing tag $TAG"
git push origin "$TAG"

echo "Waiting for GitHub Release $TAG to be created by Actions"
for ((attempt=1; attempt<=MAX_ATTEMPTS; attempt++)); do
  if gh release view "$TAG" >/dev/null 2>&1; then
    echo "Release found. Updating notes from $NOTES_FILE"
    gh release edit "$TAG" --title "$TAG" --notes-file "$NOTES_FILE"
    echo "Release $TAG updated successfully."
    exit 0
  fi

  sleep "$SLEEP_SECONDS"
done

echo "Timed out waiting for release $TAG to appear." >&2
echo "The tag was pushed. If the workflow is still running, update the notes later with:" >&2
echo "  gh release edit \"$TAG\" --title \"$TAG\" --notes-file \"$NOTES_FILE\"" >&2
exit 1
