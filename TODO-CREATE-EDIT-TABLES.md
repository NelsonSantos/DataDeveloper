# TODO - Create/Edit Tables

## Done in the first create-table pass

- Create a new table from the schema explorer.
- Define table schema/name.
- Define columns with provider-specific data types.
- Expose nullable, primary key, identity, length, precision, scale, and default expression options.
- Generate provider-specific `create table`, foreign key, and index SQL.
- Create primary keys from one or more checked columns.
- Create foreign keys with one or more local/referenced column mappings.
- Create indexes with one or more columns and unique option.
- Show generated SQL in AvaloniaEdit.
- Show warnings/errors below the SQL preview as a bullet list.
- Keep valid SQL visible when FK/index definitions are incomplete or invalid.
- Apply the generated SQL and refresh the schema explorer on success.
- Preserve FK/index combo selections when switching tabs.
- Expose index column sort direction in the UI.
- Allow manual primary key name.
- Allow reordering table columns with drag-and-drop.

## Still useful in the first phase

- Keep local validation focused on designer/model consistency. Provider-specific SQL validity should be enforced by the database when the generated script is applied.
- Review and complete provider data type catalogs:
  - SQL Server: data type coverage, identity-capable types, datetime scale rules.
  - MySQL: unsigned types, text/blob variants, charset/collation options later.
  - PostgreSQL: identity vs serial strategy, arrays/ranges later.
  - Oracle: identity support, number precision/scale defaults, timestamp variants.
  - SQLite: affinity behavior and special primary key/autoincrement rules.
- Add UI tests or view-model tests for:
  - FK/index combo selections surviving tab changes.
  - clearing `On Delete` / `On Update`.
  - SQL preview keeping valid SQL while invalid FK/index rows show warnings.
- Manual provider validation against real databases for SQL Server, MySQL, PostgreSQL, Oracle, and SQLite.

## Later create-table enhancements

`TableDefinition`, `TableColumnDefinition`, and `TableIndexDefinition` each expose a
`ProviderOptions` dictionary (string key/value) as the intended extension point for the
items below, instead of adding first-class properties per provider to the shared models.
`TableDdlScriptBuilder` does not read these dictionaries yet; a provider implementation
should read only the keys it defines and ignore the rest.

- SQL Server index options:
  - clustered/nonclustered;
  - included columns;
  - filtered index;
  - fill factor.
- PostgreSQL index options:
  - partial index;
  - method selection such as btree/gin/gist/hash;
  - expression indexes.
- MySQL table/index options:
  - storage engine;
  - charset/collation;
  - index prefix length.
- Oracle options:
  - tablespace;
  - index tablespace;
  - sequence/trigger strategy if needed for older versions.
- SQLite options:
  - `without rowid`;
  - generated columns if needed.
- Column-level extras:
  - computed/generated columns;
  - comments/descriptions;
  - check constraints;
  - unique constraints;
  - collation per column;
  - default expression helpers per provider.

## Edit existing table phase

### Done in the phase-1 safe subset

- Load an existing table definition into the designer (`TableDefinitionLoader`, structured
  `GetColumnDefaultValueStatement`/`GetPrimaryKeyStatement`/`GetForeignKeyStatement`/
  `GetIndexStatement` queries per provider).
- Keep an immutable original model (`_originalDefinition`) and an editable current model,
  via `TableDesignerViewModel.CreateForEdit`.
- Generate a structured diff between original/current models and provider-specific
  `alter table` scripts (`TableDdlScriptBuilder.BuildAlterTableScript`) for:
  - add/drop columns;
  - add/drop primary key (whole-constraint replace, no partial column diff);
  - add/drop foreign key;
  - add/drop index.
- Handle destructive changes with an explicit itemized warning dialog before Apply.
- Apply edit-table scripts inside an explicit transaction
  (`TabConnectionViewModel.ExecuteBackgroundStatementTransactionallyAsync`); the existing
  create-table apply path is untouched.
- Refresh the schema explorer after a successful alter script (reuses the existing
  `refreshSchema: true` apply path).
- "Edit table..." entry in the schema explorer context menu for `NodeType.Table`.
- Existing columns are read-only in the designer grid (only removable); newly added columns
  during an edit session remain fully editable. Table/schema name are read-only in edit mode.

### Done in phase 2 (full parity)

- Alter an existing column's type/length/precision/scale/nullable/default (`ALTER COLUMN` on SQL
  Server, `MODIFY COLUMN` on MySQL, three separate `ALTER COLUMN ... TYPE`/`SET NOT NULL`/
  `SET DEFAULT` statements on Postgres, `MODIFY (...)` on Oracle).
- Rename a column (`sp_rename` on SQL Server, `CHANGE COLUMN` on MySQL, `RENAME COLUMN` on
  Postgres/Oracle) and rename the table (`sp_rename` on SQL Server, `RENAME TO` elsewhere).
- SQLite: full rebuild procedure (temp table + `INSERT ... SELECT` column mapping + drop + rename
  + recreate indexes, wrapped in `pragma foreign_keys=off/on`) whenever the diff is anything
  beyond pure column additions — SQLite now has parity with the other 4 providers for every
  phase-1 and phase-2 diff type.
- Index options via `TableIndexDefinition.ProviderOptions`/`TableIndexColumnDefinition.ProviderOptions`:
  SQL Server clustered + fill factor, PostgreSQL index method (`using`) + partial-index `where`
  predicate, MySQL per-column prefix length. Loaded back and round-tripped by
  `TableDefinitionLoader` when reopening an existing index for editing.

### Explicitly out of scope for phase 2 (identity toggling on existing columns)

- Toggling identity/auto-increment on an *existing* column is not diffed, even though
  MySQL/Postgres could support it directly — kept uniformly out of scope since SQL Server and
  Oracle cannot do this via a simple ALTER. The Identity checkbox stays locked for existing
  columns in the designer grid.
- Renaming a column that participates in a foreign key or index does not propagate the new name
  into the FK/index definitions automatically — renaming such a column and applying will still
  reference the old column name in FK/index clauses. Needs its own follow-up if this becomes a
  common workflow.
- Schema moves (renaming/moving a table to a different schema) remain out of scope.
- Table-level provider options (MySQL storage engine/charset, Oracle tablespace, SQLite
  `without rowid`) and column extras (check constraints, comments, generated columns, collation)
  remain backlog items — the `ProviderOptions` extension point exists on `TableDefinition`/
  `TableColumnDefinition` for when they're picked up.

## ANTLR usage candidates

- Validate generated SQL syntax per provider when grammar support is strong enough.
- Parse user-edited SQL preview if the preview becomes editable later.
- Parse existing table DDL for round-trip scenarios when provider metadata is insufficient.
- Keep ANTLR ownership in `DataDeveloper.Antlr`; do not duplicate generated parser code in other projects.
