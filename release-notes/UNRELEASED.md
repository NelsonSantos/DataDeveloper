# Release Notes — Unreleased

## Summary
- **#44 fix: keep routine bodies intact when they contain CASE expressions** (https://github.com/NelsonSantos/DataDeveloper/pull/44)
  - `StatementSplitter` tracked `BEGIN`/`END` to avoid splitting routine bodies on `;`, but the `END` of a `CASE ... END` expression also closed the block. The body was then cut at the next `;`, so SQL Server received a truncated `CREATE PROCEDURE` (e.g. `Incorrect syntax near '@pointOfSaleId'`).
  - `CASE` now opens a block closed by its `END` (including `END CASE`).
  - `END IF` / `END LOOP` / `END WHILE` / `END REPEAT` no longer close an outer `BEGIN` (MySQL/Oracle).
  - `BEGIN TRY/CATCH ... END TRY/CATCH` keep working.
- **#45 refactor: share identifier quoting through a per-provider SQL dialect** (https://github.com/NelsonSantos/DataDeveloper/pull/45)
  - Phase 1 of consolidating schema metadata access into a single per-provider point in `DataDeveloper.Data`.
  - Identifier quoting, qualified-name splitting and parameter prefixes were implemented three times: in `TableDdlScriptBuilder`, `EditableResultSetCommandBuilder` and `DatabaseObjectScriptBuilder`. This PR introduces `ISqlDialect`, with one implementation per supported provider, and routes all three builders through it.
  - `ISqlDialect` (`Interfaces/`) plus the shared base `SqlDialect` (`Services/SqlDialects/`), resolved with `SqlDialect.For(DatabaseType)`.
  - One dialect per provider folder: `SqlServerSqlDialect`, `OracleSqlDialect`, `PostgresSqlDialect`, `MySqlSqlDialect` and `SqLiteSqlDialect`.
  - `QuoteIdentifier` always delimits and keeps the exact case, for names read from the catalog.
  - `FormatIdentifier` is the form used in generated DDL. On Oracle it leaves regular, non-reserved identifiers unquoted and upper-cased. The Oracle reserved-word list moved from `TableDdlScriptBuilder` to `OracleSqlDialect`.
  - `SplitQualifiedName` and `QuoteQualifiedName` split on dots outside `[]`, `""` and ``` `` ``` delimiters.
  - `FormatParameterReference` uses `:` on Oracle and `@` on the other providers.

## Included Commits
- ab6c278 Merge pull request #45 from NelsonSantos/feature/schema-metadata-foundation
- c14c5a8 Merge pull request #44 from NelsonSantos/feature/splitter-case-end
