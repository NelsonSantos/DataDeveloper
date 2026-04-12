using DataDeveloper.Data.Enums;

namespace DataDeveloper.Data.Services;

public sealed class OracleSqlAnalyzer : ProviderSqlAnalyzer
{
    public OracleSqlAnalyzer()
        : base(DatabaseType.Oracle)
    {
    }

    public override bool IsBeginTransactionStatement(string statement)
    {
        return false;
    }

    public override bool RequiresMaterialization(string statement)
    {
        var tokens = Tokenize(statement);
        if (tokens.Count == 0)
            return false;

        return StartsWithKeyword(statement, "call") ||
               (StartsWithKeyword(statement, "begin") && ContainsRoutineInvocation(tokens));
    }
}
