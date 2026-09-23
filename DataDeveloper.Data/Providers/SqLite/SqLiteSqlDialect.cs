using DataDeveloper.Data.Services.SqlDialects;

namespace DataDeveloper.Data.Providers.SqLite;

public sealed class SqLiteSqlDialect : SqlDialect
{
    public override string QuoteIdentifier(string identifier) => Delimit(identifier, '"', '"');
}
