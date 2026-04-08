using System.Text;
using Antlr4.Runtime;
using SqlServer;

namespace DataDeveloper.Data.Services;

public class StatementSplitter
{
    public static List<string> SplitStatements(string sqlText)
    {
        var statements = new List<string>();

        foreach (var batch in SplitOracleClientBatches(sqlText))
        {
            if (!ContainsExecutableTokens(batch))
                continue;

            if (IsOracleRoutineBatch(batch))
            {
                statements.Add(batch.Trim());
                continue;
            }

            if (IsOracleAnonymousBlockBatch(batch))
            {
                statements.Add(batch.Trim());
                continue;
            }

            statements.AddRange(SplitStandardStatements(batch));
        }

        return statements;
    }

    private static IEnumerable<string> SplitOracleClientBatches(string sqlText)
    {
        var batches = new List<string>();
        var current = new StringBuilder();
        var lines = sqlText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        foreach (var line in lines)
        {
            if (string.Equals(line.Trim(), "/", StringComparison.Ordinal))
            {
                if (current.Length > 0)
                {
                    batches.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.AppendLine(line);
        }

        if (current.Length > 0)
            batches.Add(current.ToString());

        return batches;
    }

    private static IEnumerable<string> SplitStandardStatements(string sqlText)
    {
        var input = new AntlrInputStream(sqlText);
        var lexer = new TSqlLexer(input);
        var tokens = new CommonTokenStream(lexer);
        tokens.Fill();

        var statements = new List<string>();
        var allTokens = tokens.GetTokens();

        int? startIndex = null;
        int? endIndex = null;
        var blockLevel = 0;

        foreach (var token in allTokens)
        {
            if (token.Type == TSqlLexer.BEGIN)
            {
                blockLevel++;
            }
            else if (token.Type == TSqlLexer.END)
            {
                if (blockLevel > 0)
                    blockLevel--;
            }

            if ((token.Type == TSqlLexer.GO || token.Type == TSqlLexer.SEMI) && blockLevel == 0)
            {
                FlushStatement(sqlText, statements, startIndex, endIndex, includeDelimiter: token.Type == TSqlLexer.SEMI);
                startIndex = null;
                endIndex = null;
                continue;
            }

            if (token.Type != TokenConstants.EOF)
            {
                startIndex ??= token.StartIndex;
                endIndex = token.StopIndex;
            }
        }

        FlushStatement(sqlText, statements, startIndex, endIndex, includeDelimiter: false);
        return statements;
    }

    private static void FlushStatement(string sqlText, ICollection<string> statements, int? startIndex, int? endIndex, bool includeDelimiter)
    {
        if (startIndex is null || endIndex is null)
            return;

        var length = endIndex.Value - startIndex.Value + 1;
        var statement = sqlText.Substring(startIndex.Value, length);
        if (includeDelimiter && ShouldKeepTrailingSemicolon(statement))
            statement += ";";

        if (ContainsExecutableTokens(statement))
            statements.Add(statement.Trim());
    }

    private static bool ContainsExecutableTokens(string statement)
    {
        if (string.IsNullOrWhiteSpace(statement) || IsOracleClientDelimiter(statement))
            return false;

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

    private static bool IsOracleClientDelimiter(string statement)
    {
        return string.Equals(statement.Trim(), "/", StringComparison.Ordinal);
    }

    private static bool IsOracleRoutineBatch(string statement)
    {
        var keywords = ReadLeadingKeywords(statement, 4);
        if (keywords.Count < 2)
            return false;

        if (!string.Equals(keywords[0], "create", StringComparison.OrdinalIgnoreCase))
            return false;

        var kindIndex = 1;
        if (keywords.Count >= 4 &&
            string.Equals(keywords[1], "or", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(keywords[2], "replace", StringComparison.OrdinalIgnoreCase))
        {
            kindIndex = 3;
        }

        return kindIndex < keywords.Count &&
               (string.Equals(keywords[kindIndex], "procedure", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(keywords[kindIndex], "function", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldKeepTrailingSemicolon(string statement)
    {
        var keywords = ReadLeadingKeywords(statement, 1);
        if (keywords.Count == 0)
            return false;

        return string.Equals(keywords[0], "begin", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(keywords[0], "declare", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOracleAnonymousBlockBatch(string statement)
    {
        return ShouldKeepTrailingSemicolon(statement) &&
               statement.TrimEnd().EndsWith("end;", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> ReadLeadingKeywords(string statement, int maxKeywords)
    {
        var keywords = new List<string>(maxKeywords);
        var index = 0;

        while (index < statement.Length && keywords.Count < maxKeywords)
        {
            while (index < statement.Length && char.IsWhiteSpace(statement[index]))
                index++;

            if (index + 1 < statement.Length && statement[index] == '-' && statement[index + 1] == '-')
            {
                index += 2;
                while (index < statement.Length && statement[index] != '\n')
                    index++;
                continue;
            }

            if (index + 1 < statement.Length && statement[index] == '/' && statement[index + 1] == '*')
            {
                index += 2;
                while (index + 1 < statement.Length && !(statement[index] == '*' && statement[index + 1] == '/'))
                    index++;

                if (index + 1 < statement.Length)
                    index += 2;

                continue;
            }

            if (index >= statement.Length || !char.IsLetter(statement[index]))
                break;

            var start = index++;
            while (index < statement.Length && char.IsLetter(statement[index]))
                index++;

            keywords.Add(statement[start..index]);
        }

        return keywords;
    }
}
