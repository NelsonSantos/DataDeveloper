using Antlr4.Runtime;
using SqlServer;

public static class SqlContextAnalyzer
{
    public static SqlContextResult Analyze(string sql, int caretPosition)
    {
        var input = new AntlrInputStream(sql);
        var lexer = new TSqlLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new TSqlParser(tokens);
        parser.RemoveErrorListeners();

        var tree = parser.tsql_file();
        var visitor = new SqlCaretContextVisitor(caretPosition, tokens);
        visitor.Visit(tree);
        return visitor.Result;
    }
}