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
- **#38 feat: reveal current file in Finder/Explorer and add Open Recent menu** (https://github.com/NelsonSantos/DataDeveloper/pull/38)
  - Adds a **Reveal in Finder** / **Show in Explorer** / **Open Containing Folder** item (label picked per OS) to the File menu, right after Save as..., which opens the OS file manager on the currently selected tab's file. Enabled only when that tab has a file that actually exists on disk.
  - Adds an **Open Recent** submenu, placed before Open, listing up to the 20 most recently opened/saved files (most recent first, no duplicate entries), with a **Clear items** action at the bottom. Disabled when there are no recent files or no connection open to attach the file to. Clicking an entry whose file no longer exists removes it from the list and shows a message instead of throwing.
  - Recent files are tracked on File > Open and on successful Save/Save As, persisted to `recent-files.json` via the same `AppDataFileService` JSON pattern already used for window state (`IRecentFilesService`/`RecentFilesService`).
  - The Open Recent submenu is populated dynamically in code-behind when opened (mirrors the existing dynamic-menu pattern used for the schema explorer's context menu); a placeholder child item is kept in XAML so Avalonia actually renders the submenu as expandable — the empty menu never opened.

## Included Commits
- cbae25c Merge pull request #38 from NelsonSantos/feature/reveal-file-in-explorer
- 7ea97f9 Merge pull request #37 from NelsonSantos/feature/generate-guid-tool
- 0c9cc00 Merge pull request #36 from NelsonSantos/feature/fix-ddl-new-query-save
