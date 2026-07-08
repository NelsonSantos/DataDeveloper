# Release Notes — Unreleased

## Summary
- **#34 fix: Cmd/Ctrl+C copies from whichever pane has focus** (https://github.com/NelsonSantos/DataDeveloper/pull/34)
  - The Edit menu's Copy `HotKey` always intercepted Cmd/Ctrl+C at the window level based on a `CanCopy` flag that only ever tracked the SQL editor, so once the editor had a selection, the shortcut never reached the NextGrid — clicking a cell and pressing Cmd/Ctrl+C silently did nothing.
  - `MainWindowViewModel` now tracks which pane (SQL editor or grid) currently has focus and routes the `Copy` command to whichever one is active, instead of always assuming the editor. The menu's `HotKey` stays exactly where it was.
  - `NextGridControl`/`NextGridPresenter` expose `CanCopy` and `CopySelectionAsync()` publicly so the window-level command can query and trigger grid copy the same way it already does for the editor.
  - `TabDataGridView` now reports grid focus to `MainWindowViewModel`, mirroring the existing SQL editor wiring.
- **#35 fix: parameters panel paste focus + value/null persistence** (https://github.com/NelsonSantos/DataDeveloper/pull/35)
  - The window's Cut/Copy/Paste/Undo/Redo shortcuts stuck to the SQL editor once it gained focus and were never released when focus moved to another control in the same tab (e.g. a query parameter's value `TextBox`), so Ctrl/Cmd+V kept pasting into the SQL editor instead of the focused parameter field. `TabQueryEditorView` now clears the editor as the active clipboard target whenever focus moves to any other in-view control (except the results grid, which manages its own target), letting that control handle its own paste.
  - Query parameter values and their "Send as Null" checkbox reset whenever the SQL statement was edited, even for edits unrelated to that parameter, or edits where the parameter is briefly removed and then re-added. `RefreshDetectedParameters` now reuses the existing `QueryParameterValue` instance when a parameter survives an edit, and remembers each parameter's last known value/null-state by name so it's restored even after the parameter briefly disappears from detection.

## Included Commits
- b458485 Merge pull request #35 from NelsonSantos/feature/fix-parameters-panel-paste-focus
- 167e436 Merge pull request #34 from NelsonSantos/feature/fix-grid-copy-focus
