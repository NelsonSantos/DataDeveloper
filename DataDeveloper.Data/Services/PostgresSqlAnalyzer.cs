using DataDeveloper.Data.Enums;

namespace DataDeveloper.Data.Services;

public sealed class PostgresSqlAnalyzer : ProviderSqlAnalyzer
{
    public PostgresSqlAnalyzer()
        : base(DatabaseType.PostgresSql)
    {
    }

    public override bool IsBeginTransactionStatement(string statement)
    {
        return StartsWithKeyword(statement, "begin") ||
               StartsWithKeywords(statement, "start", "transaction");
    }

    public override bool RequiresMaterialization(string statement)
    {
        var tokens = Tokenize(statement);
        if (tokens.Count == 0)
            return false;

        return StartsWithKeyword(statement, "call");
    }
}
