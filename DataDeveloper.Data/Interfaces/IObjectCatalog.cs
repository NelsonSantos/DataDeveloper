using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Interfaces;

/// <summary>
/// Provider-specific SQL for reading database object metadata.
/// </summary>
/// <remarks>
/// Column and table structure statements take a <c>SchemaName</c> and a <c>TableName</c> parameter. A null
/// <c>SchemaName</c> means the connection's default schema. Each returns rows shaped like the
/// matching <see cref="TableStructure"/> model.
/// </remarks>
public interface IObjectCatalog
{
    /// <summary>
    /// Object kinds shown as folders under the connection, in display order.
    /// </summary>
    IReadOnlyList<DbObjectKind> RootObjectKinds { get; }

    /// <summary>
    /// One row per object of the given kind: Name, SchemaName, IsDefaultSchema and, for routines,
    /// SpecificName and (functions) DataType, or Details for kinds that show extra information.
    /// See <see cref="DatabaseObjectModel"/>.
    /// </summary>
    string GetObjectListStatement(DbObjectKind kind);

    /// <summary>
    /// One row per column of a table or view (SchemaName, TableName parameters), shaped like
    /// <see cref="ColumnModel"/>.
    /// </summary>
    string GetColumnsStatement();

    /// <summary>
    /// One row per routine parameter for the routine's SpecificName parameter, shaped like
    /// <see cref="RoutineParameterModel"/>.
    /// </summary>
    string GetRoutineParametersStatement();

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

    /// <summary>
    /// One row per unique constraint column: ConstraintName, ColumnName, OrdinalPosition.
    /// </summary>
    string GetUniqueConstraintsStatement();

    /// <summary>
    /// How to read the table's check constraints, excluding NOT NULL constraints, or null when
    /// the server version does not store check constraints.
    /// </summary>
    /// <param name="serverVersion">The connection's <see cref="System.Data.Common.DbConnection.ServerVersion"/>.</param>
    CheckConstraintsQuery? GetCheckConstraintsQuery(string serverVersion);

    /// <summary>
    /// How to list the triggers defined on a table (SchemaName, TableName parameters).
    /// </summary>
    TriggersQuery GetTriggersQuery();
}
