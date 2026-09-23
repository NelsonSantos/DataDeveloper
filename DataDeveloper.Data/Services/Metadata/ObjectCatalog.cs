using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.Oracle;
using DataDeveloper.Data.Providers.PostgresSql;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Data.Services.SqlDialects;

namespace DataDeveloper.Data.Services.Metadata;

public abstract class ObjectCatalog : IObjectCatalog
{
    private static readonly IObjectCatalog SqlServer = new SqlServerObjectCatalog();
    private static readonly IObjectCatalog Oracle = new OracleObjectCatalog();
    private static readonly IObjectCatalog PostgresSql = new PostgresObjectCatalog();
    private static readonly IObjectCatalog MySql = new MySqlObjectCatalog();
    private static readonly IObjectCatalog SqLite = new SqLiteObjectCatalog();

    protected ObjectCatalog(DatabaseType databaseType)
    {
        Dialect = SqlDialect.For(databaseType);
    }

    protected ISqlDialect Dialect { get; }

    public static IObjectCatalog For(DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.SqlServer => SqlServer,
            DatabaseType.Oracle => Oracle,
            DatabaseType.PostgresSql => PostgresSql,
            DatabaseType.MySql => MySql,
            DatabaseType.SqLite => SqLite,
            _ => throw new ArgumentOutOfRangeException(nameof(databaseType), databaseType, "Unsupported database type.")
        };
    }

    public DdlRetrieval? GetDdlRetrieval(DbObjectRef databaseObject)
    {
        return databaseObject.Kind switch
        {
            DbObjectKind.Table => GetTableDdlRetrieval(databaseObject),
            DbObjectKind.View => GetViewDdlRetrieval(databaseObject),
            DbObjectKind.Procedure => GetRoutineDdlRetrieval(databaseObject, isFunction: false),
            DbObjectKind.Function => GetRoutineDdlRetrieval(databaseObject, isFunction: true),
            DbObjectKind.Trigger => GetTriggerDdlRetrieval(databaseObject),
            _ => null
        };
    }

    protected abstract DdlRetrieval? GetTableDdlRetrieval(DbObjectRef table);

    protected abstract DdlRetrieval? GetViewDdlRetrieval(DbObjectRef view);

    protected abstract DdlRetrieval? GetRoutineDdlRetrieval(DbObjectRef routine, bool isFunction);

    /// <param name="trigger">A trigger reference whose <see cref="DbObjectRef.Parent"/> is its table.</param>
    protected abstract DdlRetrieval GetTriggerDdlRetrieval(DbObjectRef trigger);

    public virtual IReadOnlyList<DbObjectKind> RootObjectKinds { get; } =
        [DbObjectKind.Table, DbObjectKind.View, DbObjectKind.Procedure, DbObjectKind.Function];

    public abstract string GetObjectListStatement(DbObjectKind kind);

    public abstract string GetColumnsStatement();

    public abstract string GetRoutineParametersStatement();

    public abstract string GetColumnDefaultsStatement();

    public abstract string GetPrimaryKeyStatement();

    public abstract string GetForeignKeysStatement();

    public abstract string GetIndexesStatement();

    public abstract string GetUniqueConstraintsStatement();

    public abstract CheckConstraintsQuery? GetCheckConstraintsQuery(string serverVersion);

    public abstract TriggersQuery GetTriggersQuery();

    protected string QuoteQualifiedName(DbObjectRef databaseObject)
    {
        return databaseObject.Schema is null
            ? Dialect.QuoteIdentifier(databaseObject.Name)
            : $"{Dialect.QuoteIdentifier(databaseObject.Schema)}.{Dialect.QuoteIdentifier(databaseObject.Name)}";
    }

    protected static string EscapeSqlLiteral(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }
}
