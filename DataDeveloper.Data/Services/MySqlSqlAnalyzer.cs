using DataDeveloper.Data.Enums;

namespace DataDeveloper.Data.Services;

public sealed class MySqlSqlAnalyzer : ProviderSqlAnalyzer
{
    public MySqlSqlAnalyzer()
        : base(DatabaseType.MySql)
    {
    }

    public override bool IsBeginTransactionStatement(string statement)
    {
        return StartsWithKeywords(statement, "start", "transaction") ||
               StartsWithKeyword(statement, "begin");
    }

    public override bool IsDmlStatement(string statement)
    {
        return base.IsDmlStatement(statement) ||
               StartsWithKeywords(statement, "replace", "into");
    }

    public override bool RequiresMaterialization(string statement)
    {
        var tokens = Tokenize(statement);
        if (tokens.Count == 0)
            return false;

        return StartsWithKeyword(statement, "call") ||
               (StartsWithKeyword(statement, "begin") && ContainsRoutineInvocation(tokens) && !IsBeginTransactionStatement(statement));
    }
}
