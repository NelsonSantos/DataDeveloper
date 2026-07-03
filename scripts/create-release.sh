#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  ./scripts/create-release.sh <version> [notes-file]

Example:
  ./scripts/create-release.sh 26.0330.1
  ./scripts/create-release.sh 26.0330.1 release-notes/26.0330.1.md

What it does:
  1. If <notes-file> is omitted, uses release-notes/UNRELEASED.md when present
     (kept up to date automatically by CI on every push to main). Otherwise,
     generates release-notes/<version>.md via scripts/generate-release-notes.sh
     (summarizing the pull requests merged since the last release tag).
  2. Creates an annotated git tag: v<version>
  3. Pushes the tag to origin
  4. Waits for the GitHub Release to be created by Actions
  5. Replaces the auto-generated release notes with the contents of the notes file

  After the release is published, CI archives release-notes/UNRELEASED.md as
  release-notes/<version>.md and resets it for the next cycle.

Requirements:
  - git
  - gh (GitHub CLI) authenticated for this repository
EOF
}

if [[ $# -lt 1 || $# -gt 2 ]]; then
  usage
  exit 1
fi

VERSION="$1"
TAG="v${VERSION}"
MAX_ATTEMPTS="${MAX_ATTEMPTS:-120}"
SLEEP_SECONDS="${SLEEP_SECONDS:-5}"
TRIGGER_GRACE_ATTEMPTS="${TRIGGER_GRACE_ATTEMPTS:-4}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

NOTES_FILE="${2:-}"
if [[ -z "$NOTES_FILE" ]]; then
  if [[ -f "release-notes/UNRELEASED.md" ]]; then
    NOTES_FILE="release-notes/UNRELEASED.md"
  else
    NOTES_FILE="release-notes/${VERSION}.md"
  fi
fi

if [[ ! -f "$NOTES_FILE" ]]; then
  if [[ $# -eq 2 ]]; then
    echo "Notes file not found: $NOTES_FILE" >&2
    exit 1
  fi

  echo "Notes file not found: $NOTES_FILE. Generating it automatically." >&2
  "$SCRIPT_DIR/generate-release-notes.sh" "$VERSION" "$NOTES_FILE"
  echo "" >&2
  echo "Generated $NOTES_FILE. Review it, then commit it before continuing:" >&2
  echo "  git add $NOTES_FILE && git commit -m \"docs: add release notes for $VERSION\"" >&2
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

find_release_run_id() {
  gh run list --workflow release.yml --limit 20 --json databaseId,displayTitle,event \
    --jq ".[] | select(.displayTitle == \"$TAG\" and .event == \"push\") | .databaseId" \
    | head -n 1
}

echo "Checking whether the tag push triggered the Release workflow"
RUN_ID=""
for ((attempt=1; attempt<=TRIGGER_GRACE_ATTEMPTS; attempt++)); do
  if gh release view "$TAG" >/dev/null 2>&1; then
    echo "Release found. Updating notes from $NOTES_FILE"
    gh release edit "$TAG" --title "$TAG" --notes-file "$NOTES_FILE"
    echo "Release $TAG updated successfully."
    exit 0
  fi

  RUN_ID="$(find_release_run_id || true)"
  if [[ -n "$RUN_ID" ]]; then
    echo "Detected Release workflow run $RUN_ID from tag push."
    break
  fi

  sleep "$SLEEP_SECONDS"
done

if [[ -z "$RUN_ID" ]]; then
  echo "Tag push did not trigger Release automatically. Dispatching workflow manually."
  gh workflow run release.yml -f version="$VERSION" -f create_release=true
fi

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
