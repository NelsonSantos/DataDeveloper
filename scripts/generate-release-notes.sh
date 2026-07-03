#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  ./scripts/generate-release-notes.sh <version> [output-file]

Example:
  ./scripts/generate-release-notes.sh 26.0703.0
  ./scripts/generate-release-notes.sh 26.0703.0 release-notes/26.0703.0.md

What it does:
  1. Finds the most recent existing "v*" tag (the previous release).
  2. Lists pull requests merged into main since that tag.
  3. Extracts the "## Summary" section from each pull request's body.
  4. Writes a consolidated release notes file (default: release-notes/<version>.md).

The output file is only written to disk; it is not committed automatically.
Review it and commit it before running create-release.sh.

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
OUTPUT_FILE="${2:-release-notes/${VERSION}.md}"

if ! command -v gh >/dev/null 2>&1; then
  echo "GitHub CLI 'gh' is required." >&2
  exit 1
fi

PREVIOUS_TAG="$(git tag --list 'v*' --sort=-v:refname | head -n 1 || true)"

if [[ -n "$PREVIOUS_TAG" ]]; then
  echo "Previous release tag: $PREVIOUS_TAG" >&2
  COMMIT_RANGE="${PREVIOUS_TAG}..HEAD"
else
  echo "No previous release tag found; using full history." >&2
  COMMIT_RANGE="HEAD"
fi

extract_summary() {
  awk '
    /^## Summary[ \t]*$/ { capture=1; next }
    /^#{1,6}[ \t]/ { if (capture) capture=0 }
    capture {
      line=$0
      gsub(/\r$/, "", line)
      if (line ~ /^[ \t]*$/) next
      if (line ~ /^[ \t]*<!--/) next
      sub(/^[ \t]*[-*][ \t]+/, "", line)
      sub(/^[ \t]+/, "", line)
      print "  - " line
    }
  '
}

PR_NUMBERS=()
while IFS= read -r number; do
  [[ -n "$number" ]] && PR_NUMBERS+=("$number")
done < <(git log "$COMMIT_RANGE" --merges --format='%s' | grep -oE 'Merge pull request #[0-9]+' | grep -oE '[0-9]+' || true)

SUMMARY_SECTION=""
if [[ ${#PR_NUMBERS[@]} -eq 0 ]]; then
  echo "No merged pull requests found since ${PREVIOUS_TAG:-the start of history}." >&2
  SUMMARY_SECTION="- No summarized changes found."$'\n'
else
  # PR_NUMBERS comes back newest-first from git log; walk it back to front
  # so the summary reads in the order the pull requests actually merged.
  for (( idx=${#PR_NUMBERS[@]}-1; idx>=0; idx-- )); do
    PR_NUMBER="${PR_NUMBERS[$idx]}"
    PR_JSON="$(gh pr view "$PR_NUMBER" --json title,body,url)"
    PR_TITLE="$(jq -r '.title' <<< "$PR_JSON")"
    PR_URL="$(jq -r '.url' <<< "$PR_JSON")"
    PR_BODY="$(jq -r '.body // ""' <<< "$PR_JSON")"

    SUMMARY_SECTION+="- **#${PR_NUMBER} ${PR_TITLE}** (${PR_URL})"$'\n'

    PR_SUMMARY_ITEMS="$(extract_summary <<< "$PR_BODY")"
    if [[ -n "$PR_SUMMARY_ITEMS" ]]; then
      SUMMARY_SECTION+="${PR_SUMMARY_ITEMS}"$'\n'
    fi
  done
fi

COMMITS_SECTION="$(git log "$COMMIT_RANGE" --merges --format='- %h %s' || true)"
if [[ -z "$COMMITS_SECTION" ]]; then
  COMMITS_SECTION="- No merge commits found."
fi

mkdir -p "$(dirname "$OUTPUT_FILE")"

{
  echo "# Release Notes — ${VERSION}"
  echo ""
  echo "## Summary"
  printf '%s' "$SUMMARY_SECTION"
  echo ""
  echo "## Included Commits"
  echo "$COMMITS_SECTION"
} > "$OUTPUT_FILE"

echo "Wrote $OUTPUT_FILE" >&2
echo "Review the file, then commit it before running create-release.sh." >&2
