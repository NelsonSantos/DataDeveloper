using System.Data;
using System.Data.Common;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Interfaces;

public interface IDatabaseProvider
{
    DbConnection GetConnection();
    TestConnectionResult TestConnection();
    IReadOnlyList<string> GetAvailableDatabaseNames();
    string GetTableStatement();
    string GetViewStatement();
    string GetColumnStatement();
    string GetProcedureStatement();
    string GetFunctionStatement();
    string GetRoutineParameterStatement();

    /// <summary>
    /// Statement returning one row per column with its default value expression text
    /// (columns: ColumnName, DefaultValueExpression). Used by the table designer's
    /// edit-table metadata loader; kept separate from <see cref="GetColumnStatement"/>
    /// so the widely used schema-tree column loading path is not affected.
    /// </summary>
    string GetColumnDefaultValueStatement();

    /// <summary>
    /// Statement returning one row per primary key column (columns: ConstraintName,
    /// ColumnName, OrdinalPosition), ordered by OrdinalPosition. SQLite has no named PK
    /// constraint, so ConstraintName may be empty there.
    /// </summary>
    string GetPrimaryKeyStatement();

    /// <summary>
    /// Statement returning one row per foreign key column (columns: ConstraintName,
    /// ColumnName, OrdinalPosition, ReferencedSchemaName, ReferencedTableName,
    /// ReferencedColumnName, OnDeleteAction, OnUpdateAction), ordered by constraint
    /// then OrdinalPosition.
    /// </summary>
    string GetForeignKeyStatement();

    /// <summary>
    /// Statement returning one row per index column (columns: IndexName, IsUnique,
    /// ColumnName, IsDescending, OrdinalPosition), excluding the backing index of the
    /// primary key/unique constraints, ordered by index name then OrdinalPosition.
    /// </summary>
    string GetIndexStatement();
}

public interface IDatabaseProvider<TConnectionSettings> : IDatabaseProvider 
    where TConnectionSettings : IConnectionSettings
{
    TConnectionSettings ConnectionSettings { get; }
}
