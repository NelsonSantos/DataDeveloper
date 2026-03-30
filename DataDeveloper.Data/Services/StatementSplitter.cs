using Antlr4.Runtime;
using SqlServer;

namespace DataDeveloper.Data.Services;

public class StatementSplitter
{
    public static List<string> SplitStatements(string sqlText)
    {
        var input = new AntlrInputStream(sqlText);
        var lexer = new TSqlLexer(input);
        var tokens = new CommonTokenStream(lexer);
        tokens.Fill();

        var statements = new List<string>();
        var allTokens = tokens.GetTokens();

        List<IToken> buffer = new();
        int? startIndex = null;
        int? endIndex = null;
        int blockLevel = 0;

        foreach (var token in allTokens)
        {
            var tokenText = token.Text?.ToUpperInvariant();

            // Detecta BEGIN/END para pilha de blocos
            if (token.Type == TSqlLexer.BEGIN)
            {
                blockLevel++;
            }
            else if (token.Type == TSqlLexer.END)
            {
                if (blockLevel > 0)
                    blockLevel--;
            }

            // Detecção segura de GO somente fora de blocos
            if ((token.Type == TSqlLexer.GO || token.Type == TSqlLexer.SEMI) && blockLevel == 0)
            {
                if (startIndex != null && endIndex != null)
                {
                    var statement = sqlText.Substring(startIndex.Value, endIndex.Value - startIndex.Value + 1);
                    if (ContainsExecutableTokens(statement))
                        statements.Add(statement.Trim());
                }
                startIndex = null;
                endIndex = null;
                continue;
            }

            // Marcação de início/fim do statement
            if (token.Type != TokenConstants.EOF)
            {
                if (startIndex == null)
                    startIndex = token.StartIndex;

                endIndex = token.StopIndex;
            }
        }

        // Final flush (último bloco sem GO)
        if (startIndex != null && endIndex != null)
        {
            var statement = sqlText.Substring(startIndex.Value, endIndex.Value - startIndex.Value + 1);
            if (ContainsExecutableTokens(statement))
                statements.Add(statement.Trim());
        }

        return statements;
    }

    private static bool ContainsExecutableTokens(string statement)
    {
        var input = new AntlrInputStream(statement);
        var lexer = new TSqlLexer(input);
        var tokens = new CommonTokenStream(lexer);
        tokens.Fill();

        foreach (var token in tokens.GetTokens())
        {
            if (token.Type == TokenConstants.EOF ||
                token.Type == TSqlLexer.SPACE ||
                token.Type == TSqlLexer.COMMENT ||
                token.Type == TSqlLexer.LINE_COMMENT)
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
// public class StatementSplitter
// {
//     public static List<string> SplitStatements(string sqlText)
//     {
//         var input = new AntlrInputStream(sqlText);
//         var lexer = new TSqlLexer(input);
//         var tokens = new CommonTokenStream(lexer);
//         tokens.Fill();
//
//         var statements = new List<string>();
//         List<IToken> buffer = new();
//         int? startIndex = null;
//         int? endIndex = null;
//
//         foreach (var token in tokens.GetTokens())
//         {
//             if (token.Type == TSqlLexer.SEMI || token.Type == TSqlLexer.GO)
//             {
//                 if (startIndex != null && endIndex != null)
//                 {
//                     var statement = sqlText.Substring(startIndex.Value, endIndex.Value - startIndex.Value + 1);
//                     if (!string.IsNullOrWhiteSpace(statement))
//                         statements.Add(statement.Trim());
//                 }
//                 startIndex = null;
//                 endIndex = null;
//             }
//             else if (token.Type != TokenConstants.EOF || token.Type == TSqlLexer.END)
//             {
//                 if (startIndex == null)
//                     startIndex = token.StartIndex;
//
//                 endIndex = token.StopIndex;
//             }
//         }
//
//         // Final flush (sem ; ou GO no final)
//         if (startIndex != null && endIndex != null)
//         {
//             var statement = sqlText.Substring(startIndex.Value, endIndex.Value - startIndex.Value + 1);
//             if (!string.IsNullOrWhiteSpace(statement))
//                 statements.Add(statement.Trim());
//         }
//
//         return statements;
//     }    
// }
