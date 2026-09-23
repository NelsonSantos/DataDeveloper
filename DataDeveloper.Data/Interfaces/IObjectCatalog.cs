using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Interfaces;

/// <summary>
/// Provider-specific SQL for reading database object metadata.
/// </summary>
/// <remarks>
/// Table structure statements take a <c>SchemaName</c> and a <c>TableName</c> parameter. A null
/// <c>SchemaName</c> means the connection's default schema. Each returns rows shaped like the
/// matching <see cref="TableStructure"/> model.
/// </remarks>
public interface IObjectCatalog
{
    /// <summary>
    /// Returns how to read the object's native DDL, or null when the provider has no such object.
    /// </summary>
    DdlRetrieval? GetDdlRetrieval(DbObjectRef databaseObject);

    /// <summary>
    /// One row per column: ColumnName, DefaultValueExpression and, where the provider names
    /// default constraints, DefaultConstraintName.
    /// </summary>
    string GetColumnDefaultsStatement();

    /// <summary>
    /// One row per primary key column: ConstraintName, ColumnName, OrdinalPosition.
    /// </summary>
    string GetPrimaryKeyStatement();

    /// <summary>
    /// One row per foreign key column: ConstraintName, ColumnName, OrdinalPosition,
    /// ReferencedSchemaName, ReferencedTableName, ReferencedColumnName, OnDeleteAction, OnUpdateAction.
    /// </summary>
    string GetForeignKeysStatement();

    /// <summary>
    /// One row per index column (IndexName, IsUnique, ColumnName, IsDescending, OrdinalPosition and
    /// optional provider options), excluding indexes backing primary key or unique constraints.
    /// </summary>
    string GetIndexesStatement();
}
