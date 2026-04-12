using Antlr4.Runtime;
using DataDeveloper.Data.Enums;
using SqlServer;

namespace DataDeveloper.Data.Services;

public static class ProviderSqlLexerFactory
{
    public static Lexer Create(DatabaseType databaseType, string sql)
    {
        var input = new AntlrInputStream(sql);
        return databaseType switch
        {
            DatabaseType.SqlServer => new TSqlLexer(input),
            DatabaseType.MySql => new MySQLLexer(input),
            DatabaseType.PostgresSql => new PostgreSQLLexer(input),
            DatabaseType.Oracle => new PlSqlLexer(input),
            DatabaseType.SqLite => new SQLiteLexer(input),
            _ => throw new NotSupportedException($"Database type {databaseType} is not implemented")
        };
    }
}
