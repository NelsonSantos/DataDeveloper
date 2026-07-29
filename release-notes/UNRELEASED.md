# Release Notes — Unreleased

## Summary
- **#40 feat: add CSV/XLS/XLSX file import wizard** (https://github.com/NelsonSantos/DataDeveloper/pull/40)
  - Adds a step-by-step wizard (Connection → File → Target → Columns → Review → Done, mirroring the Compare Schemas UX) to import data from CSV/XLS/XLSX files into a **new** table or an **existing** one, with column mapping and per-provider type suggestions.
  - Reachable from two entry points: the "Import file" button on a connection tab's toolbar (connection pre-selected) and the Tools menu "Import File..." (user picks the connection).
  - Reuses existing infrastructure end to end: `TableDdlScriptBuilder` for CREATE TABLE, `ProviderDataTypeCatalog` for type suggestions, and `EditableResultSetCommandBuilder` + `IStatementExecutor` for parameterized, provider-safe batch inserts — so all 5 supported providers (SQL Server, MySQL, PostgreSQL, Oracle, SQLite) work from the start.
  - Batch inserts roll back and resume in a fresh transaction on a per-row failure, since some providers (PostgreSQL) abort the whole transaction after a single failed statement.
  - Normalizes blank cells and the literal text "NULL" to an actual database NULL, and sanitizes file-name-derived table name suggestions (a dot in the file name was previously misread as a schema separator).
  - Result screen shows a green/yellow/red status icon depending on whether the import fully succeeded, partially succeeded, or failed entirely.

## Included Commits
- 919c4cb Merge pull request #40 from NelsonSantos/feature/file-import
