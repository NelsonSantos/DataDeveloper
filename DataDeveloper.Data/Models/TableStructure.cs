namespace DataDeveloper.Data.Models;

/// <summary>
/// Catalog rows describing a table's column defaults, primary key, foreign keys and indexes.
/// Multi-column constraints and indexes have one row per column.
/// </summary>
public sealed class TableStructure
{
    public IReadOnlyList<ColumnDefaultValueModel> ColumnDefaults { get; init; } = [];
    public IReadOnlyList<PrimaryKeyColumnModel> PrimaryKeyColumns { get; init; } = [];
    public IReadOnlyList<ForeignKeyColumnModel> ForeignKeyColumns { get; init; } = [];

    /// <summary>
    /// Indexes other than the ones backing the primary key or unique constraints.
    /// </summary>
    public IReadOnlyList<IndexColumnModel> IndexColumns { get; init; } = [];
}

/// <summary>
/// Catalog rows for a table's keys, one row per constraint column.
/// </summary>
public sealed class TableKeys
{
    public IReadOnlyList<PrimaryKeyColumnModel> PrimaryKeyColumns { get; init; } = [];
    public IReadOnlyList<UniqueConstraintColumnModel> UniqueConstraintColumns { get; init; } = [];
    public IReadOnlyList<ForeignKeyColumnModel> ForeignKeyColumns { get; init; } = [];
}

public sealed class ColumnDefaultValueModel
{
    public string ColumnName { get; set; } = string.Empty;
    public string? DefaultValueExpression { get; set; }
    public string? DefaultConstraintName { get; set; }
}

public sealed class PrimaryKeyColumnModel
{
    /// <summary>
    /// Empty on SQLite, which has no named primary key constraint.
    /// </summary>
    public string? ConstraintName { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public int OrdinalPosition { get; set; }
}

public sealed class ForeignKeyColumnModel
{
    public string ConstraintName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public int OrdinalPosition { get; set; }
    public string? ReferencedSchemaName { get; set; }
    public string? ReferencedTableName { get; set; }
    public string ReferencedColumnName { get; set; } = string.Empty;
    public string? OnDeleteAction { get; set; }
    public string? OnUpdateAction { get; set; }
}

public sealed class IndexColumnModel
{
    public string IndexName { get; set; } = string.Empty;
    public bool IsUnique { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public bool IsDescending { get; set; }
    public int OrdinalPosition { get; set; }
    public bool? IsClustered { get; set; }
    public int? FillFactor { get; set; }
    public string? UsingMethod { get; set; }
    public string? WherePredicate { get; set; }
    public int? PrefixLength { get; set; }
}

public sealed class UniqueConstraintColumnModel
{
    /// <summary>
    /// On SQLite, the name of the index backing the constraint.
    /// </summary>
    public string ConstraintName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public int OrdinalPosition { get; set; }
}

public sealed class CheckConstraintModel
{
    /// <summary>
    /// Empty for an unnamed SQLite check constraint.
    /// </summary>
    public string ConstraintName { get; set; } = string.Empty;

    /// <summary>
    /// The check expression, without the CHECK keyword.
    /// </summary>
    public string Definition { get; set; } = string.Empty;
}
