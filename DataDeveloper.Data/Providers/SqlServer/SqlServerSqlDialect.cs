using DataDeveloper.Data.Services.SqlDialects;

namespace DataDeveloper.Data.Providers.SqlServer;

public sealed class SqlServerSqlDialect : SqlDialect
{
    public override string QuoteIdentifier(string identifier) => Delimit(identifier, '[', ']');
}
