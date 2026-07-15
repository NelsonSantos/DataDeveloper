# Release Notes — Unreleased

## Summary
- **#36 fix: DDL create-to-new-query tabs closed silently instead of prompting Save As** (https://github.com/NelsonSantos/DataDeveloper/pull/36)
  - Right-clicking a database object → SQL Scripts → "DDL create to new query" opens a new editor tab with no backing file. Pressing Save (menu or Ctrl+S) on that tab silently closed it instead of opening the Save As dialog.
  - Root cause: `SaveCurrentEditorTab` routed any tab without an existing on-disk file into `CloseTabQueryEditor` instead of `SaveChanges`, and `OpenQueryEditorWithScript` marked the freshly-generated tab as "not changed" even though it had unsaved content and no file — so the close routine's unsaved-changes prompt was skipped entirely.
  - Fix: `SaveCurrentEditorTab` now always calls `SaveChanges`, which already knows how to choose between saving directly and showing Save As. `OpenQueryEditorWithScript` no longer forces `TextWasChanged = false`, so DDL-generated tabs are correctly treated as dirty until saved.

## Included Commits
- 0c9cc00 Merge pull request #36 from NelsonSantos/feature/fix-ddl-new-query-save
