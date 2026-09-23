using DataDeveloper.Data.Services.SqlDialects;

namespace DataDeveloper.Data.Providers.PostgresSql;

public sealed class PostgresSqlDialect : SqlDialect
{
    public override string QuoteIdentifier(string identifier) => Delimit(identifier, '"', '"');
}
