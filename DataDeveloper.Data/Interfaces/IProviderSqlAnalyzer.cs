using DataDeveloper.Data.Services;

namespace DataDeveloper.Data.Interfaces;

public interface IProviderSqlAnalyzer
{
    IReadOnlyList<string> SplitStatements(string sqlText);
    bool RequiresMaterialization(string statement);
    bool RequiresSchemaRefresh(string statement);
    bool IsDmlStatement(string statement);
    bool IsTransactionControlStatement(string statement);
    bool IsBeginTransactionStatement(string statement);
    SchemaRefreshTarget? ParseSchemaRefreshTarget(string statement);
}
