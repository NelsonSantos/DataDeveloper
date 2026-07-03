# Release Notes — Unreleased

## Summary
- **#32 Pin published release notes heading to the actual version** (https://github.com/NelsonSantos/DataDeveloper/pull/32)
  - `create-release.sh` now writes a temp copy of the notes file with the first line rewritten to `# Release Notes — <version>` and passes that to `gh release edit --notes-file`, instead of the source file as-is.
  - Fixes the v26.0703.0 release body showing `# Release Notes — Unreleased` (inherited from `release-notes/UNRELEASED.md`'s placeholder heading) instead of the actual version.

## Included Commits
- a0998e7 Merge pull request #32 from NelsonSantos/feature/fix-release-notes-heading
