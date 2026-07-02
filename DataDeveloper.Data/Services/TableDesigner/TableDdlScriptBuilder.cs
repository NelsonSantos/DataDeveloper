using System.Text;
using System.Text.RegularExpressions;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models.TableDesigner;

namespace DataDeveloper.Data.Services.TableDesigner;

public static partial class TableDdlScriptBuilder
{
    public static string BuildCreateTableScript(DatabaseType databaseType, TableDefinition table)
    {
        Validate(table);

        var lines = new List<string>();
        var inlineSqliteIdentityPk = TryGetInlineSqliteIdentityPrimaryKey(databaseType, table);

        foreach (var column in table.Columns)
            lines.Add("    " + BuildColumnDefinition(databaseType, table, column, inlineSqliteIdentityPk));

        if (table.PrimaryKey.ColumnNames.Count > 0 && inlineSqliteIdentityPk is null)
            lines.Add("    " + BuildPrimaryKeyDefinition(databaseType, table));

        foreach (var foreignKey in table.ForeignKeys.Where(IsUsableForeignKey))
            lines.Add("    " + BuildForeignKeyDefinition(databaseType, foreignKey));

        var builder = new StringBuilder();
        builder.AppendLine($"create table {BuildQualifiedName(databaseType, table.SchemaName, table.TableName)}");
        builder.AppendLine("(");
        builder.AppendLine(string.Join("," + Environment.NewLine, lines));
        builder.Append(ShouldUseMySqlInnoDb(databaseType, table) ? ") engine=InnoDB;" : ");");

        foreach (var index in table.Indexes.Where(index => index.Columns.Count > 0))
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.Append(BuildIndexDefinition(databaseType, table, index));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Builds an ALTER TABLE script diffing <paramref name="original"/> against
    /// <paramref name="current"/>. Covers: add/drop column, alter an existing column's
    /// type/length/precision/scale/nullable/default (identity is intentionally excluded from
    /// the diff — SQL Server and Oracle cannot toggle IDENTITY on an existing column via a
    /// simple ALTER, so it is kept out of scope uniformly rather than only supporting it for
    /// MySQL/Postgres), rename column, rename table, add/drop primary key (whole-constraint
    /// replace), add/drop foreign key, add/drop index. For SQLite (which has no ALTER support
    /// for any of the above beyond ADD COLUMN) this delegates to
    /// <see cref="BuildSqLiteAlterOrRebuildScript"/>.
    /// </summary>
    public static string BuildAlterTableScript(DatabaseType databaseType, TableDefinition original, TableDefinition current)
    {
        if (databaseType == DatabaseType.SqLite)
            return BuildSqLiteAlterOrRebuildScript(original, current);

        var statements = new List<string>();
        var tableRenamed = !string.Equals(original.TableName, current.TableName, StringComparison.OrdinalIgnoreCase);

        if (tableRenamed)
            statements.Add(BuildRenameTableStatement(databaseType, original, current));

        var qualifiedName = tableRenamed
            ? BuildQualifiedName(databaseType, current.SchemaName, current.TableName)
            : BuildQualifiedName(databaseType, original.SchemaName, original.TableName);

        var originalByKey = original.Columns.ToDictionary(column => column.Name, StringComparer.OrdinalIgnoreCase);
        var matchedOriginalNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in current.Columns)
        {
            var matchKey = column.OriginalName ?? column.Name;
            if (originalByKey.TryGetValue(matchKey, out var originalColumn))
            {
                matchedOriginalNames.Add(matchKey);
                AppendColumnAlterStatements(databaseType, qualifiedName, originalColumn, column, statements);
            }
            else
            {
                statements.Add(
                    $"alter table {qualifiedName} {BuildAddColumnClause(databaseType)} {BuildColumnDefinition(databaseType, current, column, null)};");
            }
        }

        foreach (var column in original.Columns.Where(column => !matchedOriginalNames.Contains(column.Name)))
            statements.Add($"alter table {qualifiedName} drop column {QuoteIdentifier(databaseType, column.Name)};");

        AppendPrimaryKeyDiff(databaseType, qualifiedName, original, current, statements);
        AppendForeignKeyDiff(databaseType, qualifiedName, original, current, statements);
        AppendIndexDiff(databaseType, qualifiedName, original, current, statements);

        return string.Join(Environment.NewLine + Environment.NewLine, statements);
    }

    private static bool HasColumnTypeChange(DatabaseType databaseType, TableColumnDefinition original, TableColumnDefinition current)
    {
        if (!string.Equals(original.DataType, current.DataType, StringComparison.OrdinalIgnoreCase))
            return true;

        // Only compare Length/Precision/Scale for facets the resolved type actually declares
        // support for (matching how BuildDefinition assembles "current"). Several providers
        // report a non-zero internal storage size for types with no length concept at all (for
        // example SQL Server's sys.columns.max_length is non-zero even for decimal/datetime2/
        // uniqueidentifier columns) — comparing those raw numbers against "current" (which is
        // always null for a type that doesn't support Length) would treat every such column as
        // changed even when nothing was edited.
        var typeOption = ProviderDataTypeCatalog.GetDataTypes(databaseType)
            .FirstOrDefault(option => string.Equals(option.Name, current.DataType, StringComparison.OrdinalIgnoreCase));

        return (typeOption?.SupportsLength == true && original.Length != current.Length) ||
               (typeOption?.SupportsPrecision == true && original.Precision != current.Precision) ||
               (typeOption?.SupportsScale == true && original.Scale != current.Scale);
    }

    private static bool HasColumnTypeOrNullableChange(DatabaseType databaseType, TableColumnDefinition original, TableColumnDefinition current)
    {
        return HasColumnTypeChange(databaseType, original, current) || original.IsNullable != current.IsNullable;
    }

    private static bool HasColumnDefaultChange(TableColumnDefinition original, TableColumnDefinition current)
    {
        // Identity columns never carry an explicit default (mirrors BuildColumnDefinition's
        // "!column.IsIdentity" guard for CREATE). Some providers (Oracle) also report the
        // underlying system-generated sequence expression as the loaded column's data_default,
        // which would otherwise look like a spurious default change and produce an invalid
        // "identity column cannot have a default value" ALTER.
        if (current.IsIdentity)
            return false;

        return !string.Equals((original.DefaultValue ?? string.Empty).Trim(), (current.DefaultValue ?? string.Empty).Trim(), StringComparison.Ordinal);
    }

    private static void AppendColumnAlterStatements(
        DatabaseType databaseType, string qualifiedName, TableColumnDefinition original, TableColumnDefinition current, List<string> statements)
    {
        var renamed = !string.Equals(original.Name, current.Name, StringComparison.OrdinalIgnoreCase);
        var typeOrNullableChanged = HasColumnTypeOrNullableChange(databaseType, original, current);
        var defaultChanged = HasColumnDefaultChange(original, current);

        if (!renamed && !typeOrNullableChanged && !defaultChanged)
            return;

        switch (databaseType)
        {
            case DatabaseType.SqlServer:
                if (renamed)
                    statements.Add(BuildSqlServerRenameColumnStatement(qualifiedName, original.Name, current.Name));
                if (typeOrNullableChanged)
                {
                    statements.Add(
                        $"alter table {qualifiedName} alter column {QuoteIdentifier(databaseType, current.Name)} {BuildDataType(current)} {(current.IsNullable ? "null" : "not null")};");
                }
                if (defaultChanged)
                    AppendSqlServerDefaultChange(databaseType, qualifiedName, original, current, statements);
                break;

            case DatabaseType.MySql:
                // MODIFY/CHANGE COLUMN carries the full column definition in one statement;
                // CHANGE COLUMN is required (over MODIFY) whenever the name changes.
                var mysqlDefinition = BuildColumnDefinition(databaseType, null!, current, null);
                statements.Add(renamed
                    ? $"alter table {qualifiedName} change column {QuoteIdentifier(databaseType, original.Name)} {mysqlDefinition};"
                    : $"alter table {qualifiedName} modify column {mysqlDefinition};");
                break;

            case DatabaseType.PostgresSql:
                if (renamed)
                    statements.Add($"alter table {qualifiedName} rename column {QuoteIdentifier(databaseType, original.Name)} to {QuoteIdentifier(databaseType, current.Name)};");

                var postgresColumnName = QuoteIdentifier(databaseType, current.Name);
                if (HasColumnTypeChange(databaseType, original, current))
                    statements.Add($"alter table {qualifiedName} alter column {postgresColumnName} type {BuildDataType(current)};");
                if (original.IsNullable != current.IsNullable)
                    statements.Add($"alter table {qualifiedName} alter column {postgresColumnName} {(current.IsNullable ? "drop not null" : "set not null")};");
                if (defaultChanged)
                {
                    statements.Add(string.IsNullOrWhiteSpace(current.DefaultValue)
                        ? $"alter table {qualifiedName} alter column {postgresColumnName} drop default;"
                        : $"alter table {qualifiedName} alter column {postgresColumnName} set default {current.DefaultValue.Trim()};");
                }
                break;

            case DatabaseType.Oracle:
                if (renamed)
                    statements.Add($"alter table {qualifiedName} rename column {QuoteIdentifier(databaseType, original.Name)} to {QuoteIdentifier(databaseType, current.Name)};");
                if (typeOrNullableChanged || defaultChanged)
                {
                    var modifyParts = new List<string>
                    {
                        QuoteIdentifier(databaseType, current.Name),
                        BuildDataType(current)
                    };

                    if (defaultChanged)
                    {
                        modifyParts.Add(string.IsNullOrWhiteSpace(current.DefaultValue)
                            ? "default null"
                            : "default " + current.DefaultValue.Trim());
                    }

                    // Oracle rejects a redundant "modify (col null)" when the column is already
                    // nullable (ORA-01451) — only assert nullability when it actually changed.
                    if (original.IsNullable != current.IsNullable)
                        modifyParts.Add(current.IsNullable ? "null" : "not null");

                    statements.Add($"alter table {qualifiedName} modify ({string.Join(" ", modifyParts)});");
                }
                break;
        }
    }

    private static string BuildSqlServerRenameColumnStatement(string qualifiedName, string oldName, string newName)
    {
        // sp_rename is a stored procedure call, not a standard ALTER TABLE statement.
        return $"exec sp_rename '{qualifiedName.Replace("'", "''", StringComparison.Ordinal)}.{oldName.Replace("'", "''", StringComparison.Ordinal)}', '{newName.Replace("'", "''", StringComparison.Ordinal)}', 'COLUMN';";
    }

    private static void AppendSqlServerDefaultChange(
        DatabaseType databaseType, string qualifiedName, TableColumnDefinition original, TableColumnDefinition current, List<string> statements)
    {
        if (original.ProviderOptions.TryGetValue("default_constraint_name", out var existingConstraintName) &&
            !string.IsNullOrWhiteSpace(existingConstraintName))
        {
            statements.Add($"alter table {qualifiedName} drop constraint {QuoteIdentifier(databaseType, existingConstraintName)};");
        }

        if (!string.IsNullOrWhiteSpace(current.DefaultValue))
        {
            statements.Add(
                $"alter table {qualifiedName} add default {current.DefaultValue.Trim()} for {QuoteIdentifier(databaseType, current.Name)};");
        }
    }

    private static string BuildRenameTableStatement(DatabaseType databaseType, TableDefinition original, TableDefinition current)
    {
        var originalQualifiedName = BuildQualifiedName(databaseType, original.SchemaName, original.TableName);

        return databaseType switch
        {
            // sp_rename is a stored procedure call, not a standard ALTER TABLE statement.
            DatabaseType.SqlServer => $"exec sp_rename '{originalQualifiedName.Replace("'", "''", StringComparison.Ordinal)}', '{current.TableName.Replace("'", "''", StringComparison.Ordinal)}';",
            _ => $"alter table {originalQualifiedName} rename to {QuoteIdentifier(databaseType, current.TableName)};"
        };
    }

    /// <summary>
    /// SQLite has no ALTER support beyond ADD COLUMN. When the diff is only additions, emit the
    /// lightweight ADD COLUMN statements (cheap, no data movement). Otherwise, follow SQLite's
    /// documented rebuild procedure: create a new table under a temp name with the desired
    /// (<paramref name="current"/>) schema, copy matching data across via INSERT/SELECT (mapping
    /// renamed columns by <see cref="TableColumnDefinition.OriginalName"/>, omitting dropped
    /// columns, omitting new columns unless they have a default), drop the original table,
    /// rename the temp table into place, and recreate indexes — all wrapped in a transaction
    /// with foreign key checks disabled for the duration.
    /// </summary>
    private static string BuildSqLiteAlterOrRebuildScript(TableDefinition original, TableDefinition current)
    {
        var originalByKey = original.Columns.ToDictionary(column => column.Name, StringComparer.OrdinalIgnoreCase);
        var matchedOriginalNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var onlyAdditions = true;

        foreach (var column in current.Columns)
        {
            var matchKey = column.OriginalName ?? column.Name;
            if (originalByKey.TryGetValue(matchKey, out var originalColumn))
            {
                matchedOriginalNames.Add(matchKey);
                if (!string.Equals(originalColumn.Name, column.Name, StringComparison.OrdinalIgnoreCase) ||
                    HasColumnTypeOrNullableChange(DatabaseType.SqLite, originalColumn, column) || HasColumnDefaultChange(originalColumn, column))
                {
                    onlyAdditions = false;
                }
            }
        }

        var droppedColumns = original.Columns.Where(column => !matchedOriginalNames.Contains(column.Name)).ToList();
        var primaryKeyChanged = !original.PrimaryKey.ColumnNames.SequenceEqual(current.PrimaryKey.ColumnNames, StringComparer.OrdinalIgnoreCase);
        var foreignKeysChanged = !original.ForeignKeys.Where(IsUsableForeignKey).Select(fk => fk.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(current.ForeignKeys.Where(IsUsableForeignKey).Select(fk => fk.Name));
        var indexesChanged = !original.Indexes.Where(index => index.Columns.Count > 0).Select(index => index.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(current.Indexes.Where(index => index.Columns.Count > 0).Select(index => index.Name));
        var tableRenamed = !string.Equals(original.TableName, current.TableName, StringComparison.OrdinalIgnoreCase);

        if (onlyAdditions && droppedColumns.Count == 0 && !primaryKeyChanged && !foreignKeysChanged && !indexesChanged && !tableRenamed)
        {
            var addStatements = current.Columns
                .Where(column => !matchedOriginalNames.Contains(column.OriginalName ?? column.Name) && !originalByKey.ContainsKey(column.Name))
                .Select(column => $"alter table {QuoteIdentifier(DatabaseType.SqLite, original.TableName)} add column {BuildColumnDefinition(DatabaseType.SqLite, current, column, null)};");

            return string.Join(Environment.NewLine + Environment.NewLine, addStatements);
        }

        var tempTableName = "temp_" + current.TableName;
        var rebuildDefinition = new TableDefinition
        {
            SchemaName = current.SchemaName,
            TableName = tempTableName
        };
        rebuildDefinition.Columns.AddRange(current.Columns);
        rebuildDefinition.PrimaryKey.Name = current.PrimaryKey.Name;
        rebuildDefinition.PrimaryKey.ColumnNames.AddRange(current.PrimaryKey.ColumnNames);
        rebuildDefinition.ForeignKeys.AddRange(current.ForeignKeys);

        var createTempTableScript = BuildCreateTableScript(DatabaseType.SqLite, rebuildDefinition);

        var currentColumnNames = new List<string>();
        var originalColumnNames = new List<string>();
        foreach (var column in current.Columns)
        {
            var matchKey = column.OriginalName ?? column.Name;
            if (!originalByKey.TryGetValue(matchKey, out var originalColumn))
                continue;

            currentColumnNames.Add(QuoteIdentifier(DatabaseType.SqLite, column.Name));
            originalColumnNames.Add(QuoteIdentifier(DatabaseType.SqLite, originalColumn.Name));
        }

        var originalQualifiedName = QuoteIdentifier(DatabaseType.SqLite, original.TableName);
        var tempQualifiedName = QuoteIdentifier(DatabaseType.SqLite, tempTableName);
        var finalTableName = QuoteIdentifier(DatabaseType.SqLite, current.TableName);

        var statements = new List<string>
        {
            "pragma foreign_keys=off;",
            createTempTableScript.TrimEnd(';', '\n', '\r') + ";",
            currentColumnNames.Count > 0
                ? $"insert into {tempQualifiedName} ({string.Join(", ", currentColumnNames)}) select {string.Join(", ", originalColumnNames)} from {originalQualifiedName};"
                : $"-- no matching columns to copy from {originalQualifiedName}",
            $"drop table {originalQualifiedName};",
            $"alter table {tempQualifiedName} rename to {finalTableName};"
        };

        foreach (var index in current.Indexes.Where(index => index.Columns.Count > 0))
            statements.Add(BuildIndexDefinition(DatabaseType.SqLite, current, index));

        statements.Add("pragma foreign_keys=on;");

        return string.Join(Environment.NewLine + Environment.NewLine, statements);
    }

    private static string BuildAddColumnClause(DatabaseType databaseType)
    {
        // SQL Server and Oracle do not accept the "column" keyword on ADD; the other
        // providers accept (Postgres/MySQL) or require (SQLite) it.
        return databaseType is DatabaseType.SqlServer or DatabaseType.Oracle ? "add" : "add column";
    }

    private static void AppendPrimaryKeyDiff(
        DatabaseType databaseType, string qualifiedName, TableDefinition original, TableDefinition current, List<string> statements)
    {
        var changed =
            !original.PrimaryKey.ColumnNames.SequenceEqual(current.PrimaryKey.ColumnNames, StringComparer.OrdinalIgnoreCase) ||
            !string.Equals(original.PrimaryKey.Name, current.PrimaryKey.Name, StringComparison.OrdinalIgnoreCase);

        if (!changed)
            return;

        if (original.PrimaryKey.ColumnNames.Count > 0 && !string.IsNullOrWhiteSpace(original.PrimaryKey.Name))
            statements.Add(BuildDropPrimaryKeyStatement(databaseType, qualifiedName, original.PrimaryKey.Name));

        if (current.PrimaryKey.ColumnNames.Count > 0)
            statements.Add($"alter table {qualifiedName} add {BuildPrimaryKeyDefinition(databaseType, current)};");
    }

    private static void AppendForeignKeyDiff(
        DatabaseType databaseType, string qualifiedName, TableDefinition original, TableDefinition current, List<string> statements)
    {
        var originalNames = original.ForeignKeys.Where(IsUsableForeignKey)
            .Select(foreignKey => foreignKey.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentNames = current.ForeignKeys.Where(IsUsableForeignKey)
            .Select(foreignKey => foreignKey.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var foreignKey in original.ForeignKeys.Where(fk => IsUsableForeignKey(fk) && !currentNames.Contains(fk.Name)))
            statements.Add(BuildDropForeignKeyStatement(databaseType, qualifiedName, foreignKey.Name));

        foreach (var foreignKey in current.ForeignKeys.Where(fk => IsUsableForeignKey(fk) && !originalNames.Contains(fk.Name)))
            statements.Add($"alter table {qualifiedName} add {BuildForeignKeyDefinition(databaseType, foreignKey)};");
    }

    private static void AppendIndexDiff(
        DatabaseType databaseType, string qualifiedName, TableDefinition original, TableDefinition current, List<string> statements)
    {
        var originalNames = original.Indexes.Where(index => index.Columns.Count > 0)
            .Select(index => index.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentNames = current.Indexes.Where(index => index.Columns.Count > 0)
            .Select(index => index.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var index in original.Indexes.Where(index => index.Columns.Count > 0 && !currentNames.Contains(index.Name)))
            statements.Add(BuildDropIndexStatement(databaseType, qualifiedName, index.Name));

        foreach (var index in current.Indexes.Where(index => index.Columns.Count > 0 && !originalNames.Contains(index.Name)))
            statements.Add(BuildIndexDefinition(databaseType, current, index));
    }

    private static string BuildDropPrimaryKeyStatement(DatabaseType databaseType, string qualifiedName, string constraintName)
    {
        return databaseType == DatabaseType.MySql
            ? $"alter table {qualifiedName} drop primary key;"
            : $"alter table {qualifiedName} drop constraint {QuoteIdentifier(databaseType, constraintName)};";
    }

    private static string BuildDropForeignKeyStatement(DatabaseType databaseType, string qualifiedName, string constraintName)
    {
        return databaseType == DatabaseType.MySql
            ? $"alter table {qualifiedName} drop foreign key {QuoteIdentifier(databaseType, constraintName)};"
            : $"alter table {qualifiedName} drop constraint {QuoteIdentifier(databaseType, constraintName)};";
    }

    private static string BuildDropIndexStatement(DatabaseType databaseType, string qualifiedName, string indexName)
    {
        return databaseType switch
        {
            DatabaseType.MySql => $"alter table {qualifiedName} drop index {QuoteIdentifier(databaseType, indexName)};",
            DatabaseType.SqlServer => $"drop index {QuoteIdentifier(databaseType, indexName)} on {qualifiedName};",
            _ => $"drop index {QuoteIdentifier(databaseType, indexName)};"
        };
    }

    private static void Validate(TableDefinition table)
    {
        if (string.IsNullOrWhiteSpace(table.TableName))
            throw new ArgumentException("Table name is required.", nameof(table));

        if (table.Columns.Count == 0)
            throw new ArgumentException("At least one column is required.", nameof(table));

        var duplicate = table.Columns
            .Where(column => !string.IsNullOrWhiteSpace(column.Name))
            .GroupBy(column => column.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
            throw new ArgumentException($"Column '{duplicate.Key}' is duplicated.", nameof(table));

        if (table.Columns.Any(column => string.IsNullOrWhiteSpace(column.Name)))
            throw new ArgumentException("Column name is required.", nameof(table));

        if (table.Columns.Any(column => string.IsNullOrWhiteSpace(column.DataType)))
            throw new ArgumentException("Column data type is required.", nameof(table));
    }

    private static string BuildColumnDefinition(
        DatabaseType databaseType,
        TableDefinition table,
        TableColumnDefinition column,
        string? inlineSqliteIdentityPk)
    {
        var parts = new List<string>
        {
            QuoteIdentifier(databaseType, column.Name),
            BuildDataType(column)
        };

        if (column.IsIdentity)
            AddIdentity(databaseType, parts);

        if (inlineSqliteIdentityPk is not null &&
            string.Equals(column.Name, inlineSqliteIdentityPk, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("primary key autoincrement");
            return string.Join(" ", parts);
        }

        parts.Add(column.IsNullable ? "null" : "not null");

        if (!string.IsNullOrWhiteSpace(column.DefaultValue) && !column.IsIdentity)
            parts.Add("default " + column.DefaultValue.Trim());

        return string.Join(" ", parts);
    }

    private static string BuildDataType(TableColumnDefinition column)
    {
        if (column.Length is > 0)
            return $"{column.DataType}({column.Length.Value})";

        if (column.Precision is > 0)
        {
            return column.Scale is > 0
                ? $"{column.DataType}({column.Precision.Value}, {column.Scale.Value})"
                : $"{column.DataType}({column.Precision.Value})";
        }

        if (column.Scale is > 0)
            return $"{column.DataType}({column.Scale.Value})";

        return column.DataType;
    }

    private static void AddIdentity(DatabaseType databaseType, List<string> parts)
    {
        switch (databaseType)
        {
            case DatabaseType.SqlServer:
                parts.Add("identity(1, 1)");
                break;
            case DatabaseType.MySql:
                parts.Add("auto_increment");
                break;
            case DatabaseType.PostgresSql:
            case DatabaseType.Oracle:
                parts.Add("generated by default as identity");
                break;
        }
    }

    private static string BuildPrimaryKeyDefinition(DatabaseType databaseType, TableDefinition table)
    {
        var columns = string.Join(", ", table.PrimaryKey.ColumnNames.Select(name => QuoteIdentifier(databaseType, name)));
        var name = string.IsNullOrWhiteSpace(table.PrimaryKey.Name)
            ? string.Empty
            : $"constraint {QuoteIdentifier(databaseType, table.PrimaryKey.Name)} ";

        return $"{name}primary key ({columns})";
    }

    private static string BuildForeignKeyDefinition(DatabaseType databaseType, TableForeignKeyDefinition foreignKey)
    {
        var localColumns = string.Join(", ", foreignKey.ColumnNames.Select(name => QuoteIdentifier(databaseType, name)));
        var referencedColumns = string.Join(", ", foreignKey.ReferencedColumnNames.Select(name => QuoteIdentifier(databaseType, name)));
        var referencedTable = BuildQualifiedName(databaseType, foreignKey.ReferencedSchemaName, foreignKey.ReferencedTableName);
        var sql = string.IsNullOrWhiteSpace(foreignKey.Name)
            ? $"foreign key ({localColumns}){Environment.NewLine}        references {referencedTable} ({referencedColumns})"
            : $"constraint {QuoteIdentifier(databaseType, foreignKey.Name)}{Environment.NewLine}        foreign key ({localColumns}){Environment.NewLine}        references {referencedTable} ({referencedColumns})";

        var actions = new List<string>();

        if (!string.IsNullOrWhiteSpace(foreignKey.OnDeleteAction))
            actions.Add($"on delete {foreignKey.OnDeleteAction.Trim().ToLowerInvariant()}");

        if (!string.IsNullOrWhiteSpace(foreignKey.OnUpdateAction) && databaseType != DatabaseType.Oracle)
            actions.Add($"on update {foreignKey.OnUpdateAction.Trim().ToLowerInvariant()}");

        if (actions.Count > 0)
            sql += $"{Environment.NewLine}        {string.Join(" ", actions)}";

        return sql;
    }

    private static string BuildIndexDefinition(DatabaseType databaseType, TableDefinition table, TableIndexDefinition index)
    {
        var unique = index.IsUnique ? "unique " : string.Empty;
        var clustered = databaseType == DatabaseType.SqlServer &&
            index.ProviderOptions.TryGetValue("clustered", out var clusteredValue) &&
            string.Equals(clusteredValue, "true", StringComparison.OrdinalIgnoreCase)
            ? "clustered "
            : string.Empty;

        var columns = string.Join(", ", index.Columns.Select(column =>
        {
            var columnName = QuoteIdentifier(databaseType, column.Name);
            if (databaseType == DatabaseType.MySql &&
                column.ProviderOptions.TryGetValue("prefix_length", out var prefixLength) &&
                !string.IsNullOrWhiteSpace(prefixLength))
            {
                columnName += $"({prefixLength})";
            }

            return columnName + (column.Descending ? " desc" : string.Empty);
        }));

        var tableName = BuildQualifiedName(databaseType, table.SchemaName, table.TableName);
        var usingClause = databaseType == DatabaseType.PostgresSql &&
            index.ProviderOptions.TryGetValue("using", out var usingMethod) &&
            !string.IsNullOrWhiteSpace(usingMethod)
            ? $"using {usingMethod.Trim().ToLowerInvariant()} "
            : string.Empty;

        var sql = $"create {unique}{clustered}index {QuoteIdentifier(databaseType, index.Name)}{Environment.NewLine}    on {tableName} {usingClause}({columns})";

        if (databaseType == DatabaseType.SqlServer &&
            index.ProviderOptions.TryGetValue("fillfactor", out var fillFactor) &&
            !string.IsNullOrWhiteSpace(fillFactor))
        {
            sql += $" with (fillfactor = {fillFactor})";
        }

        if (databaseType == DatabaseType.PostgresSql &&
            index.ProviderOptions.TryGetValue("where", out var wherePredicate) &&
            !string.IsNullOrWhiteSpace(wherePredicate))
        {
            sql += $" where {wherePredicate}";
        }

        return sql + ";";
    }

    private static bool IsUsableForeignKey(TableForeignKeyDefinition foreignKey)
    {
        return foreignKey.ColumnNames.Count > 0 &&
               !string.IsNullOrWhiteSpace(foreignKey.ReferencedTableName) &&
               foreignKey.ReferencedColumnNames.Count > 0;
    }

    private static bool ShouldUseMySqlInnoDb(DatabaseType databaseType, TableDefinition table)
    {
        return databaseType == DatabaseType.MySql && table.ForeignKeys.Any(IsUsableForeignKey);
    }

    private static string? TryGetInlineSqliteIdentityPrimaryKey(DatabaseType databaseType, TableDefinition table)
    {
        if (databaseType != DatabaseType.SqLite || table.PrimaryKey.ColumnNames.Count != 1)
            return null;

        var primaryKeyColumnName = table.PrimaryKey.ColumnNames[0];
        var column = table.Columns.FirstOrDefault(item =>
            string.Equals(item.Name, primaryKeyColumnName, StringComparison.OrdinalIgnoreCase));

        return column is { IsIdentity: true } &&
               string.Equals(column.DataType, "integer", StringComparison.OrdinalIgnoreCase)
            ? column.Name
            : null;
    }

    private static string BuildQualifiedName(DatabaseType databaseType, string schemaName, string objectName)
    {
        return string.IsNullOrWhiteSpace(schemaName) || databaseType == DatabaseType.SqLite
            ? QuoteIdentifier(databaseType, objectName)
            : $"{QuoteIdentifier(databaseType, schemaName)}.{QuoteIdentifier(databaseType, objectName)}";
    }

    private static string QuoteIdentifier(DatabaseType databaseType, string identifier)
    {
        return databaseType switch
        {
            DatabaseType.SqlServer => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]",
            DatabaseType.MySql => $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`",
            DatabaseType.Oracle => QuoteOracleIdentifier(identifier),
            _ => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
        };
    }

    private static string QuoteOracleIdentifier(string identifier)
    {
        var trimmedIdentifier = identifier.Trim();
        if (OracleRegularIdentifierRegex().IsMatch(trimmedIdentifier) && !IsOracleReservedWord(trimmedIdentifier))
            return trimmedIdentifier.ToUpperInvariant();

        return $"\"{trimmedIdentifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static bool IsOracleReservedWord(string identifier)
    {
        return OracleReservedWords.Contains(identifier.ToUpperInvariant());
    }

    private static readonly HashSet<string> OracleReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACCESS",
        "ADD",
        "ALL",
        "ALTER",
        "AND",
        "ANY",
        "AS",
        "ASC",
        "AUDIT",
        "BETWEEN",
        "BY",
        "CHAR",
        "CHECK",
        "CLUSTER",
        "COLUMN",
        "COMMENT",
        "COMPRESS",
        "CONNECT",
        "CREATE",
        "CURRENT",
        "DATE",
        "DECIMAL",
        "DEFAULT",
        "DELETE",
        "DESC",
        "DISTINCT",
        "DROP",
        "ELSE",
        "EXCLUSIVE",
        "EXISTS",
        "FILE",
        "FLOAT",
        "FOR",
        "FROM",
        "GRANT",
        "GROUP",
        "HAVING",
        "IDENTIFIED",
        "IMMEDIATE",
        "IN",
        "INCREMENT",
        "INDEX",
        "INITIAL",
        "INSERT",
        "INTEGER",
        "INTERSECT",
        "INTO",
        "IS",
        "LEVEL",
        "LIKE",
        "LOCK",
        "LONG",
        "MAXEXTENTS",
        "MINUS",
        "MLSLABEL",
        "MODE",
        "MODIFY",
        "NOAUDIT",
        "NOCOMPRESS",
        "NOT",
        "NOWAIT",
        "NULL",
        "NUMBER",
        "OF",
        "OFFLINE",
        "ON",
        "ONLINE",
        "OPTION",
        "OR",
        "ORDER",
        "PCTFREE",
        "PRIOR",
        "PRIVILEGES",
        "PUBLIC",
        "RAW",
        "RENAME",
        "RESOURCE",
        "REVOKE",
        "ROW",
        "ROWID",
        "ROWNUM",
        "ROWS",
        "SELECT",
        "SESSION",
        "SET",
        "SHARE",
        "SIZE",
        "SMALLINT",
        "START",
        "SUCCESSFUL",
        "SYNONYM",
        "SYSDATE",
        "TABLE",
        "THEN",
        "TO",
        "TRIGGER",
        "UID",
        "UNION",
        "UNIQUE",
        "UPDATE",
        "USER",
        "VALIDATE",
        "VALUES",
        "VARCHAR",
        "VARCHAR2",
        "VIEW",
        "WHENEVER",
        "WHERE",
        "WITH"
    };

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_$#]*$")]
    private static partial Regex OracleRegularIdentifierRegex();
}
