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
- **#47 refactor: load table structure through the object catalog with schema filtering** (https://github.com/NelsonSantos/DataDeveloper/pull/47)
  - Phase 3 of consolidating schema metadata access into a single per-provider point in `DataDeveloper.Data`.
  - The column default, primary key, foreign key and index statements move from `IDatabaseProvider` into each provider's `IObjectCatalog` (`GetColumnDefaultsStatement`, `GetPrimaryKeyStatement`, `GetForeignKeysStatement`, `GetIndexesStatement`).
  - `SchemaMetadataService.GetTableStructureAsync(table)` runs the four statements on one connection and returns a public `TableStructure` model. This replaces the loader's private row classes, and phase 5 will reuse it for the Keys/Indexes tree folders.
  - `TableDefinitionLoader` (Edit Table, Schema Compare of tables) now uses the service. The `TableDefinition` is assembled in a pure `Build` method.
  - `IDatabaseProvider` keeps only connection, object listing, columns and routine parameters. Columns and parameters move in phase 4.
- **#48 refactor: build the schema tree from the object catalog with schema-aware nodes** (https://github.com/NelsonSantos/DataDeveloper/pull/48)
  - This is phase 4 of consolidating schema metadata access into a single per-provider point in `DataDeveloper.Data`.
  - The object listing, column and routine parameter statements move from `IDatabaseProvider` into each provider's `IObjectCatalog` (`GetObjectListStatement(kind)`, `GetColumnsStatement`, `GetRoutineParametersStatement`). `IDatabaseProvider` now only has `GetConnection`, `TestConnection` and `GetAvailableDatabaseNames`.
  - `SchemaExplorer` builds its folders from the catalog's `RootObjectKinds`. SQLite declares only tables and views, so the `if (SqLite)` special case is gone. The explorer reads everything through `SchemaMetadataService`.
  - Column statements get the same `SchemaName`/`TableName` filtering as the phase 3 structure statements. Oracle columns now read from `all_tab_columns` filtered by owner.

## Included Commits
- dc17ff8 Merge pull request #48 from NelsonSantos/feature/schema-metadata-tree
- acf31dc Merge pull request #47 from NelsonSantos/feature/schema-metadata-table-structure
- 20e68a2 Merge pull request #46 from NelsonSantos/feature/schema-metadata-ddl
- ab6c278 Merge pull request #45 from NelsonSantos/feature/schema-metadata-foundation
- c14c5a8 Merge pull request #44 from NelsonSantos/feature/splitter-case-end
