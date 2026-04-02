using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using SqlServer;

namespace DataDeveloper.Data.Services;

public class StatementSplitter
{
    private static readonly Regex OracleRoutineStartRegex = new(
        @"^\s*create(\s+or\s+replace)?\s+(procedure|function)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OracleAnonymousBlockStartRegex = new(
        @"^\s*(begin|declare)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
        return OracleRoutineStartRegex.IsMatch(statement);
    }

    private static bool ShouldKeepTrailingSemicolon(string statement)
    {
        return OracleAnonymousBlockStartRegex.IsMatch(statement);
    }
}
