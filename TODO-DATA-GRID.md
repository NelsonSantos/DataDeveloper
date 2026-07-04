# Data Grid TODO

Backlog of grid improvements identified but deprioritized for now. Revisit later.

## Sorting

- Add click-to-sort on column headers with visual sort indicator (asc/desc/none).
- Decide whether sorting re-queries the database (ORDER BY) or sorts the already-loaded page client-side.
- Consider interaction with pagination: sorting a partially loaded result set may require reloading from the query.

## Filtering

- Add a row filter UI (e.g. per-column quick filter or a filter bar).
- Decide whether filtering rewrites the WHERE clause server-side or filters loaded rows client-side.
- Validate behavior across all supported providers if filtering translates to SQL.

## Column management

- Column reordering (drag column header to reposition).
- Column visibility toggle (hide/show columns without changing the query).
- Frozen/pinned columns support.

## Export

- Export grid contents to CSV.
- Export grid contents to JSON (see also the TODOS.md entry "Export CSV/JSON").

## Context menu

- Add a right-click context menu on the grid (copy cell/row, export, etc.) instead of relying only on keyboard shortcuts and the toolbar.

## Selection

- Multi-cell/multi-row selection (currently single row focus + single cell selection).

## Rendering/perf

- Investigate lazy/virtualized rendering for very large pages instead of rendering all visible rows eagerly.
- Revisit auto column width sizing so it isn't only computed on initial load.
