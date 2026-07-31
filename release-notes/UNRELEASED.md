# Release Notes — Unreleased

## Summary
- **#42 docs: document the correct release process in CLAUDE.md** (https://github.com/NelsonSantos/DataDeveloper/pull/42)
  - Adds a `## Releases` section to `CLAUDE.md` documenting `scripts/create-release.sh <version>` as the correct way to cut a release.
  - Explicitly calls out that triggering `release.yml` via `workflow_dispatch` (`gh workflow run`) directly is broken: its manual-dispatch path checks out the version tag before that tag exists, so every platform build fails at the checkout step. Only the tag-push path (which `create-release.sh` drives) works.
  - Prompted by hitting exactly this failure while cutting the `v26.0731.0` release.
  - Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>

## Included Commits
- 115e010 Merge pull request #42 from NelsonSantos/feature/document-release-process
