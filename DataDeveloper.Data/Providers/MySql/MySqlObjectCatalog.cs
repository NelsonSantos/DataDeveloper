using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services.Metadata;

namespace DataDeveloper.Data.Providers.MySql;

public sealed class MySqlObjectCatalog : ObjectCatalog
{
    public MySqlObjectCatalog()
        : base(DatabaseType.MySql)
    {
    }

    protected override DdlRetrieval GetTableDdlRetrieval(DbObjectRef table)
    {
        return new DdlRetrieval($"show create table {QuoteQualifiedName(table)};");
    }

    protected override DdlRetrieval GetViewDdlRetrieval(DbObjectRef view)
    {
        return new DdlRetrieval($"show create view {QuoteQualifiedName(view)};");
    }

    protected override DdlRetrieval GetRoutineDdlRetrieval(DbObjectRef routine, bool isFunction)
    {
        return new DdlRetrieval($"show create {(isFunction ? "function" : "procedure")} {QuoteQualifiedName(routine)};");
    }
}
