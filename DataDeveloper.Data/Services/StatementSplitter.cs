using Antlr4.Runtime;

namespace DataDeveloper.Data.Services;

public class StatementSplitter
{
    // public static List<string> SplitStatements(string sql)
    // {
    //     var input = new AntlrInputStream(sql);
    //     var lexer = new TSqlLexer(input);
    //     var tokens = new CommonTokenStream(lexer);
    //     var parser = new TSqlParser(tokens);
    //
    //     var listener = new StatementCollector(tokens);
    //     parser.AddParseListener(listener);
    //     parser.tsql_file();
    //
    //     return listener.Statements;
    // }    
    public static List<string> SplitStatements(string sqlText)
    {
        var input = new AntlrInputStream(sqlText);
        var lexer = new TSqlLexer(input);
        var tokens = new CommonTokenStream(lexer);
        tokens.Fill();

        var statements = new List<string>();
        List<IToken> buffer = new();
        int? startIndex = null;
        int? endIndex = null;

        foreach (var token in tokens.GetTokens())
        {
            if (token.Type == TSqlLexer.SEMI || token.Type == TSqlLexer.GO)
            {
                if (startIndex != null && endIndex != null)
                {
                    var statement = sqlText.Substring(startIndex.Value, endIndex.Value - startIndex.Value + 1);
                    if (!string.IsNullOrWhiteSpace(statement))
                        statements.Add(statement.Trim());
                }
                startIndex = null;
                endIndex = null;
            }
            else if (token.Type != TokenConstants.EOF)
            {
                if (startIndex == null)
                    startIndex = token.StartIndex;

                endIndex = token.StopIndex;
            }
        }

        // Final flush (sem ; ou GO no final)
        if (startIndex != null && endIndex != null)
        {
            var statement = sqlText.Substring(startIndex.Value, endIndex.Value - startIndex.Value + 1);
            if (!string.IsNullOrWhiteSpace(statement))
                statements.Add(statement.Trim());
        }

        return statements;
    }    
}

// public class StatementCollector : TSqlParserBaseListener
// {
//     private readonly CommonTokenStream _tokens;
//     public List<string> Statements { get; } = new();
//
//     public StatementCollector(CommonTokenStream tokens)
//     {
//         _tokens = tokens;
//     }
//
//     public override void EnterSql_clauses(TSqlParser.Sql_clausesContext context)
//     {
//         var text = _tokens.GetText(context.SourceInterval)?.Trim();
//         if (!string.IsNullOrWhiteSpace(text))
//             Statements.Add(text);        
//     }
// }
