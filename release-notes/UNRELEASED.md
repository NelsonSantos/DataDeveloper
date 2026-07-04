# Release Notes — Unreleased

## Summary
- **#32 Pin published release notes heading to the actual version** (https://github.com/NelsonSantos/DataDeveloper/pull/32)
  - `create-release.sh` now writes a temp copy of the notes file with the first line rewritten to `# Release Notes — <version>` and passes that to `gh release edit --notes-file`, instead of the source file as-is.
  - Fixes the v26.0703.0 release body showing `# Release Notes — Unreleased` (inherited from `release-notes/UNRELEASED.md`'s placeholder heading) instead of the actual version.
- **#33 Grid: fix scroll/sizing issues, add JSON/XML cell viewer** (https://github.com/NelsonSantos/DataDeveloper/pull/33)
  - **Scrolling & sizing fixes**
  - Inline cell editor now follows horizontal/vertical scroll and auto-cancels if the cell scrolls out of view (previously stayed in a stale position or floated over content).
  - Fixed an `Extent` calculation bug that was short by the row header width whenever a vertical scrollbar reserve was active — this was blocking the horizontal scroll from ever reaching the true end of content, making the last column's resize handle and cell buttons unreachable.
  - The `ScrollViewer` is now notified once column auto-width finishes computing, fixing the horizontal scrollbar not appearing on first load in some cases.
  - A column's auto-computed width is now capped at 50% of the grid width on first display, so one long value doesn't dominate the layout (manual resize is unaffected).
  - **New: JSON/XML cell viewer**
  - Every text cell shows a small "..." button. Clicking it detects whether the value is JSON or XML *at click time* (not while rendering), so it works correctly even for columns with no sampled data yet, and for columns with mixed JSON/XML content across rows.
  - Opens a dialog showing the value **exactly as stored** (no auto-formatting) with dark-theme-appropriate syntax highlighting + code folding for JSON and XML, explicit **Prettify**/**Minify** actions, a non-blocking "invalid content" warning, and Ok/Save/Cancel state that only appears once something actually changed.
  - Dialog window size is persisted across openings via the existing window-state service.
  - `TODO-DATA-GRID.md` tracks deferred grid improvements out of scope here (sorting, filtering, export, context menu, multi-select).

## Included Commits
- 26895e9 Merge pull request #33 from NelsonSantos/feature/grid-scroll-and-json-viewer
- a0998e7 Merge pull request #32 from NelsonSantos/feature/fix-release-notes-heading
