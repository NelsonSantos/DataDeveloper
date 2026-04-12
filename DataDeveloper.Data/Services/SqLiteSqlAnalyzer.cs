using DataDeveloper.Data.Enums;

namespace DataDeveloper.Data.Services;

public sealed class SqLiteSqlAnalyzer : ProviderSqlAnalyzer
{
    public SqLiteSqlAnalyzer()
        : base(DatabaseType.SqLite)
    {
    }

    public override bool IsBeginTransactionStatement(string statement)
    {
        return StartsWithKeyword(statement, "begin");
    }

    public override bool IsDmlStatement(string statement)
    {
        return base.IsDmlStatement(statement) ||
               StartsWithKeywords(statement, "replace", "into");
    }

    public override bool RequiresMaterialization(string statement)
    {
        return false;
    }
}
