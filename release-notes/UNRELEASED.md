# Release Notes — Unreleased

## Summary
- **#36 fix: DDL create-to-new-query tabs closed silently instead of prompting Save As** (https://github.com/NelsonSantos/DataDeveloper/pull/36)
  - Right-clicking a database object → SQL Scripts → "DDL create to new query" opens a new editor tab with no backing file. Pressing Save (menu or Ctrl+S) on that tab silently closed it instead of opening the Save As dialog.
  - Root cause: `SaveCurrentEditorTab` routed any tab without an existing on-disk file into `CloseTabQueryEditor` instead of `SaveChanges`, and `OpenQueryEditorWithScript` marked the freshly-generated tab as "not changed" even though it had unsaved content and no file — so the close routine's unsaved-changes prompt was skipped entirely.
  - Fix: `SaveCurrentEditorTab` now always calls `SaveChanges`, which already knows how to choose between saving directly and showing Save As. `OpenQueryEditorWithScript` no longer forces `TextWasChanged = false`, so DDL-generated tabs are correctly treated as dirty until saved.
- **#37 feat: add Tools menu with a Generate GUID window** (https://github.com/NelsonSantos/DataDeveloper/pull/37)
  - Adds a new **Tools** menu with **Generate GUID...**, opening a modeless window (stays open so you can keep pasting into the grid without reopening it) for generating one or more GUIDs to copy into GUID/UUID-typed columns.
  - Format options mirror .NET's `Guid.ToString` specifiers: hyphenated (`D`), no hyphens (`N`), braces (`B`), parentheses (`P`), plus an uppercase toggle and a quantity field (regenerates automatically on any change, or via the Regenerate button).
  - The action button is a `SplitButton`: the dropdown only *selects* the mode ("Copy" vs "Copy and close" — singular/plural label depending on quantity); the actual copy (and optional close) only runs on the next click of the primary button. The selected mode is remembered across sessions (`generate-guid-tool.json` in the app's Config folder, same pattern as `WindowStateService`).
  - Added a `SplitButton.dialog-button` style variant in `App.axaml` since `SplitButton` isn't a `Button` subtype and can't reuse the existing `Button.dialog-button` style directly.

## Included Commits
- 7ea97f9 Merge pull request #37 from NelsonSantos/feature/generate-guid-tool
- 0c9cc00 Merge pull request #36 from NelsonSantos/feature/fix-ddl-new-query-save
