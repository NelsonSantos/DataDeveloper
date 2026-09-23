using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services.Metadata;

namespace DataDeveloper.Data.Providers.SqLite;

public sealed class SqLiteObjectCatalog : ObjectCatalog
{
    public SqLiteObjectCatalog()
        : base(DatabaseType.SqLite)
    {
    }

    protected override DdlRetrieval GetTableDdlRetrieval(DbObjectRef table)
    {
        return new DdlRetrieval(BuildSqliteMasterQuery("table", table.Name));
    }

    protected override DdlRetrieval GetViewDdlRetrieval(DbObjectRef view)
    {
        return new DdlRetrieval(BuildSqliteMasterQuery("view", view.Name));
    }

    // SQLite has no stored procedures or functions.
    protected override DdlRetrieval? GetRoutineDdlRetrieval(DbObjectRef routine, bool isFunction) => null;

    private static string BuildSqliteMasterQuery(string type, string name)
    {
        return "select sql as Definition" + Environment.NewLine +
               "from sqlite_master" + Environment.NewLine +
               $"where type = '{type}' and name = '{EscapeSqlLiteral(name)}';";
    }
}
