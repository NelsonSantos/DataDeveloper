using DataDeveloper.Data.Services.SqlDialects;

namespace DataDeveloper.Data.Providers.MySql;

public sealed class MySqlSqlDialect : SqlDialect
{
    public override string QuoteIdentifier(string identifier) => Delimit(identifier, '`', '`');
}
