using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Tests;

/// <summary>
/// SQLite-compatible catalog for tests that run a schema explorer against an in-memory database.
/// Lists only the given tables: plain names are in the default "dbo" schema, "schema.name"
/// entries are in another schema.
/// </summary>
internal sealed class FakeObjectCatalog(params string[] tableNames) : IObjectCatalog
{
    private const string NoObjects = "select cast(null as text) as Name where 1 = 0";

    public IReadOnlyList<DbObjectKind> RootObjectKinds { get; } =
        [DbObjectKind.Table, DbObjectKind.View, DbObjectKind.Procedure, DbObjectKind.Function];

    public string GetObjectListStatement(DbObjectKind kind)
    {
        if (kind != DbObjectKind.Table || tableNames.Length == 0)
            return NoObjects;

        return string.Join(" union all ", tableNames.Select(entry =>
        {
            var parts = entry.Split('.');
            return parts.Length == 2
                ? $"select '{parts[1]}' as Name, '{parts[0]}' as SchemaName, 0 as IsDefaultSchema"
                : $"select '{entry}' as Name, 'dbo' as SchemaName, 1 as IsDefaultSchema";
        }));
    }

    public string GetColumnsStatement() => NoObjects;
    public string GetRoutineParametersStatement() => NoObjects;
    public DdlRetrieval? GetDdlRetrieval(DbObjectRef databaseObject) => null;
    public string GetColumnDefaultsStatement() => NoObjects;
    public string GetPrimaryKeyStatement() => NoObjects;
    public string GetForeignKeysStatement() => NoObjects;
    public string GetIndexesStatement() => NoObjects;
}
