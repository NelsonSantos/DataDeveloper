# Release Notes — Unreleased

## Summary
- **#26 Fix completion window reopening on Enter/newline** (https://github.com/NelsonSantos/DataDeveloper/pull/26)
  - `char.IsWhiteSpace` matches `\n`/`\r`/tab as well as space, so pressing Enter (or auto-indent) was treated like typing a space and could reopen or spuriously trigger the SQL completion popup.
  - Restricts the whitespace-triggered reopen/continue logic in `SqlCompletionProvider` and `CompletionInteractionState` to the literal space character only.
- **#27 Add connection grouping, TreeView, and export/import** (https://github.com/NelsonSantos/DataDeveloper/pull/27)
  - Adds a `ConnectionGroup` entity (own table, nullable FK on connections) so grouping is a real, renameable/deletable concept instead of a free-text tag; deleting a group ungroups its connections instead of orphaning data.
  - Replaces the flat connection list with a `TreeView`: groups sort first (alphabetically), then ungrouped connections, each sorted by name. Expand/collapse state persists per group.
  - Adds a "Manage groups" dialog (create/rename/delete, locked rows with explicit edit/save icons) and a per-connection Group picker.
  - Reworks the connection detail panel into General / Connection / Advanced sections, and the connection list into a bordered panel with an icon toolbar (duplicate, add, import, export, bulk delete) plus per-row checkboxes for bulk actions.
  - Adds JSON export/import for connections:
  - Export excludes passwords by default, with an explicit opt-in (warned) checkbox.
  - Export/import files carry an app + format-version marker so unrelated JSON is rejected, and older format versions stay importable.
  - Import never overwrites existing connections (name collisions get a numbered suffix) and always lands connections ungrouped, since groups are personal, local organization.
  - Fixes several UX issues surfaced while building this: nothing pre-selects when the dialog opens or the type filter changes, edit mode resets when switching the selected connection, Database/Password fields were unreadable while locked, and double-clicking a connection now connects it (also fixes a UI-thread hang during import and a crash from resolving the window off a recycled tree row).
- **#28 Auto-save query tabs per connection and restore on reopen** (https://github.com/NelsonSantos/DataDeveloper/pull/28)
  - Closing a connection tab or the whole app no longer prompts per query tab to save changes; each connection's tabs are snapshotted (one JSON file per connection, atomic write) and silently persisted instead.
  - Reopening a connection restores its previously open tabs, including unsaved edits, skipping tabs that were left empty/untouched.
  - Closing an individual query tab still prompts to save as before.
  - Opening a connection that's already open now focuses the existing tab instead of duplicating it.
- **#29 Auto-generate release notes summary when cutting a release** (https://github.com/NelsonSantos/DataDeveloper/pull/29)
  - Adds `scripts/generate-release-notes.sh <version> [output-file]`: finds the last `v*` tag, lists PRs merged into `main` since then, extracts each PR's `## Summary` section, and writes a consolidated `release-notes/<version>.md`.
  - `create-release.sh` now accepts the notes file as optional; if `release-notes/<version>.md` doesn't exist yet, it calls the new script automatically and stops, prompting to review and commit the generated file before re-running.
  - Updates `BUILDS.md` with the new step and the updated `create-release.sh` usage.
- **#31 Maintain a rolling UNRELEASED.md release notes draft via CI** (https://github.com/NelsonSantos/DataDeveloper/pull/31)
  - `release-notes.yml` now runs `scripts/generate-release-notes.sh "Unreleased" release-notes/UNRELEASED.md` on every push to `main` and commits the result alongside the existing per-commit draft, so the release notes for the next version are always up to date without anyone needing to run the script by hand.
  - `create-release.sh` now defaults to `release-notes/UNRELEASED.md` when no notes-file is given (falling back to auto-generating `release-notes/<version>.md` only if `UNRELEASED.md` doesn't exist).
  - `release.yml`'s `publish-release` job now archives `UNRELEASED.md` as `release-notes/<version>.md` and resets it after a successful release publish, committing directly to `main`.
  - Updates `BUILDS.md` to describe the new flow.

## Included Commits
- 45b91a6 Merge pull request #31 from NelsonSantos/feature/rolling-unreleased-notes
- 8113a7e Merge pull request #29 from NelsonSantos/feature/automate-release-notes-generation
- dc922f1 Merge pull request #28 from NelsonSantos/feature/tab-session-autosave
- eb87fd9 Merge pull request #27 from NelsonSantos/feature/connection-grouping
- a94bb2c Merge pull request #26 from NelsonSantos/feature/fix-completion-newline-trigger
