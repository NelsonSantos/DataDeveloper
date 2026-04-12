using DataDeveloper.Data.Enums;

namespace DataDeveloper.Data.Services;

public sealed class SqlServerSqlAnalyzer : ProviderSqlAnalyzer
{
    public SqlServerSqlAnalyzer()
        : base(DatabaseType.SqlServer)
    {
    }

    public override bool IsBeginTransactionStatement(string statement)
    {
        return StartsWithKeywords(statement, "begin", "transaction") ||
               StartsWithKeywords(statement, "begin", "tran");
    }

    public override bool RequiresMaterialization(string statement)
    {
        var tokens = Tokenize(statement);
        if (tokens.Count == 0)
            return false;

        return IsRoutineInvocation(tokens, 0) ||
               (StartsWithKeyword(statement, "begin") && ContainsRoutineInvocation(tokens));
    }
}
