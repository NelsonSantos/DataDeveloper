# Release Notes — Unreleased

## Summary
- **#42 docs: document the correct release process in CLAUDE.md** (https://github.com/NelsonSantos/DataDeveloper/pull/42)
  - Adds a `## Releases` section to `CLAUDE.md` documenting `scripts/create-release.sh <version>` as the correct way to cut a release.
  - Explicitly calls out that triggering `release.yml` via `workflow_dispatch` (`gh workflow run`) directly is broken: its manual-dispatch path checks out the version tag before that tag exists, so every platform build fails at the checkout step. Only the tag-push path (which `create-release.sh` drives) works.
  - Prompted by hitting exactly this failure while cutting the `v26.0731.0` release.
  - Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
- **#43 feat: include the version in each release installer's filename** (https://github.com/NelsonSantos/DataDeveloper/pull/43)
  - Installer filenames now embed the release version instead of being identical across every release:
  - `DataDeveloper-<version>-osx-x64.zip` / `DataDeveloper-<version>-osx-arm64.zip`
  - `DataDeveloper-<version>-win-x64-setup.exe`
  - `DataDeveloper-<version>-linux-x64.AppImage`
  - Changed `scripts/build-macos-release.sh` (zip filename), `packaging/windows/DataDeveloper.iss` (`OutputBaseFilename`), and `packaging/linux/build-appimage.sh` (new optional 3rd `version` arg, backward compatible when omitted).
  - Updated `.github/workflows/release.yml`'s three `Upload *artifact` steps to match the new filenames, and passes the resolved version into the AppImage build step.
  - Updated `README.md`'s install instructions to reflect the versioned filenames.
  - Verified the in-app "check for updates" flow (`ReleaseUpdateService`) is unaffected — it only compares the GitHub release `tag_name`, it never references an asset filename.

## Included Commits
- 3d65311 Merge pull request #43 from NelsonSantos/feature/versioned-installer-filenames
- 115e010 Merge pull request #42 from NelsonSantos/feature/document-release-process
