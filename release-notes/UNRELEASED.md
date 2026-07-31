# Release Notes — Unreleased

## Summary
- **#40 feat: add CSV/XLS/XLSX file import wizard** (https://github.com/NelsonSantos/DataDeveloper/pull/40)
  - Adds a step-by-step wizard (Connection → File → Target → Columns → Review → Done, mirroring the Compare Schemas UX) to import data from CSV/XLS/XLSX files into a **new** table or an **existing** one, with column mapping and per-provider type suggestions.
  - Reachable from two entry points: the "Import file" button on a connection tab's toolbar (connection pre-selected) and the Tools menu "Import File..." (user picks the connection).
  - Reuses existing infrastructure end to end: `TableDdlScriptBuilder` for CREATE TABLE, `ProviderDataTypeCatalog` for type suggestions, and `EditableResultSetCommandBuilder` + `IStatementExecutor` for parameterized, provider-safe batch inserts — so all 5 supported providers (SQL Server, MySQL, PostgreSQL, Oracle, SQLite) work from the start.
  - Batch inserts roll back and resume in a fresh transaction on a per-row failure, since some providers (PostgreSQL) abort the whole transaction after a single failed statement.
  - Normalizes blank cells and the literal text "NULL" to an actual database NULL, and sanitizes file-name-derived table name suggestions (a dot in the file name was previously misread as a schema separator).
  - Result screen shows a green/yellow/red status icon depending on whether the import fully succeeded, partially succeeded, or failed entirely.
- **#41 feat: add CSV/XLSX export for the result grid** (https://github.com/NelsonSantos/DataDeveloper/pull/41)
  - Adds a `SplitButton` next to "Submit" in the result grid toolbar: the main click exports the grid using the last-selected format; the dropdown flyout switches between "Export as CSV" and "Export as XLSX" without exporting.
  - The chosen format is remembered **per connection**, persisted in a small `grid-export-tool.json` settings file via `AppDataFileService` — same pattern already used by the Generate GUID tool — rather than touching the core connection-settings model/schema across all 5 providers.
  - CSV is written streaming, line by line, with proper RFC4180 quoting (commas, quotes, embedded newlines). XLSX goes through ClosedXML and writes **natively typed** cells (numbers, dates, booleans), not just the on-screen formatted text, so the exported workbook is actually sortable/summable in Excel. Cell formatting otherwise reuses `GridRendererRegistry` (the same formatting already used on screen and by copy-to-clipboard), except `byte[]` — the registry's catch-all would collapse it to the useless "System.Byte[]" text, so the exporter writes it as hex instead.
  - Since the grid loads results in pages from a still-open `DbDataReader` rather than always materializing everything up front, clicking Export while more rows are still unread prompts: load everything and export, or export only what's currently on screen.

## Included Commits
- 4585fcc Merge pull request #41 from NelsonSantos/feature/grid-export
- 919c4cb Merge pull request #40 from NelsonSantos/feature/file-import
