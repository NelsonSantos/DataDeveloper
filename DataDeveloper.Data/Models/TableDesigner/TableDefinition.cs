namespace DataDeveloper.Data.Models.TableDesigner;

public sealed class TableDefinition
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public List<TableColumnDefinition> Columns { get; } = new();
    public TablePrimaryKeyDefinition PrimaryKey { get; } = new();
    public List<TableForeignKeyDefinition> ForeignKeys { get; } = new();
    public List<TableIndexDefinition> Indexes { get; } = new();

    /// <summary>
    /// Provider-specific, table-level options not modeled as first-class properties
    /// (for example MySQL storage engine/charset or Oracle tablespace). Keys are
    /// provider-defined; <see cref="TableDdlScriptBuilder"/> ignores unknown keys until
    /// a provider implementation reads them.
    /// </summary>
    public Dictionary<string, string> ProviderOptions { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TableColumnDefinition
{
    /// <summary>
    /// The column's name as loaded from the database, when editing an existing table; null for
    /// a newly added column. Used to tell a rename (same <see cref="OriginalName"/>, different
    /// <see cref="Name"/>) apart from a drop+add pair when diffing for ALTER TABLE generation.
    /// </summary>
    public string? OriginalName { get; set; }

    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public int? Length { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public bool IsNullable { get; set; } = true;
    public bool IsIdentity { get; set; }
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>
    /// Provider-specific, column-level options not modeled as first-class properties
    /// (for example a check constraint expression, a comment, or a generated-column
    /// expression). See <see cref="TableDefinition.ProviderOptions"/> for the rationale.
    /// </summary>
    public Dictionary<string, string> ProviderOptions { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TablePrimaryKeyDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> ColumnNames { get; } = new();
}

public sealed class TableForeignKeyDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> ColumnNames { get; } = new();
    public string ReferencedSchemaName { get; set; } = string.Empty;
    public string ReferencedTableName { get; set; } = string.Empty;
    public List<string> ReferencedColumnNames { get; } = new();
    public string OnDeleteAction { get; set; } = string.Empty;
    public string OnUpdateAction { get; set; } = string.Empty;
}

public sealed class TableIndexDefinition
{
    public string Name { get; set; } = string.Empty;
    public bool IsUnique { get; set; }
    public List<TableIndexColumnDefinition> Columns { get; } = new();

    /// <summary>
    /// Provider-specific, index-level options not modeled as first-class properties
    /// (for example SQL Server clustered/fill factor, PostgreSQL method/partial predicate,
    /// or MySQL index prefix length). See <see cref="TableDefinition.ProviderOptions"/> for
    /// the rationale.
    /// </summary>
    public Dictionary<string, string> ProviderOptions { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TableIndexColumnDefinition
{
    public string Name { get; set; } = string.Empty;
    public bool Descending { get; set; }

    /// <summary>
    /// Provider-specific, per-column index options (for example MySQL index prefix length).
    /// See <see cref="TableDefinition.ProviderOptions"/> for the rationale.
    /// </summary>
    public Dictionary<string, string> ProviderOptions { get; } = new(StringComparer.OrdinalIgnoreCase);
}
