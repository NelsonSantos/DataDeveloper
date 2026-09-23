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
- **#46 refactor: read native DDL through a per-provider object catalog** (https://github.com/NelsonSantos/DataDeveloper/pull/46)
  - This is phase 2 of consolidating schema metadata access into a single per-provider point in `DataDeveloper.Data`.
  - Native DDL retrieval used to live in the UI project's `DatabaseObjectScriptBuilder`, as one `switch (DatabaseType)` per method. `TabConnectionView` executed it, including the Oracle `dbms_metadata` session setup, and `RoutineDdlRetriever` executed it a second time for Schema Compare. This PR moves all of that behind one API:
  - **`DbObjectKind` / `DbObjectRef`**: identify an object by kind, schema and name, without delimiters, instead of passing raw node-name strings around.
  - **`IObjectCatalog`**: one implementation per provider (`SqlServerObjectCatalog`, `OracleObjectCatalog`, `PostgresObjectCatalog`, `MySqlObjectCatalog`, `SqLiteObjectCatalog`). Each returns a `DdlRetrieval`, made of a query, an optional session setup and an optional post-processing step. The Oracle table DDL formatting lives here.
  - **`SchemaMetadataService.GetDdlAsync`**: the only code that runs these queries. `DdlResultReader` reads the results.
  - **Consumers**: the schema tree "DDL Create" menu and Schema Compare (views and routines) both call the service.
  - **Removed**: `RoutineDdlRetriever`, the DDL SQL in `TabConnectionView`, and the column-based `CREATE TABLE` fallback in `DatabaseObjectScriptBuilder`. That fallback never ran, because every provider has a native query. `DatabaseObjectScriptBuilder` goes from ~790 to ~245 lines.
  - The query text sent to each provider is unchanged.

## Included Commits
- 20e68a2 Merge pull request #46 from NelsonSantos/feature/schema-metadata-ddl
- ab6c278 Merge pull request #45 from NelsonSantos/feature/schema-metadata-foundation
- c14c5a8 Merge pull request #44 from NelsonSantos/feature/splitter-case-end
