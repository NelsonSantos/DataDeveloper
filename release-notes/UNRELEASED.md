# Release Notes — Unreleased

## Summary
- **#34 fix: Cmd/Ctrl+C copies from whichever pane has focus** (https://github.com/NelsonSantos/DataDeveloper/pull/34)
  - The Edit menu's Copy `HotKey` always intercepted Cmd/Ctrl+C at the window level based on a `CanCopy` flag that only ever tracked the SQL editor, so once the editor had a selection, the shortcut never reached the NextGrid — clicking a cell and pressing Cmd/Ctrl+C silently did nothing.
  - `MainWindowViewModel` now tracks which pane (SQL editor or grid) currently has focus and routes the `Copy` command to whichever one is active, instead of always assuming the editor. The menu's `HotKey` stays exactly where it was.
  - `NextGridControl`/`NextGridPresenter` expose `CanCopy` and `CopySelectionAsync()` publicly so the window-level command can query and trigger grid copy the same way it already does for the editor.
  - `TabDataGridView` now reports grid focus to `MainWindowViewModel`, mirroring the existing SQL editor wiring.

## Included Commits
- 167e436 Merge pull request #34 from NelsonSantos/feature/fix-grid-copy-focus
