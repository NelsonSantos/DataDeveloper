using System.Text;
using Antlr4.Runtime;
using DataDeveloper.Data.Enums;

namespace DataDeveloper.Data.Services;

public class StatementSplitter
{
    public static List<string> SplitStatements(string sqlText)
    {
        return SplitStatements(sqlText, DatabaseType.SqlServer);
    }

    public static List<string> SplitStatements(string sqlText, DatabaseType databaseType)
    {
        var statements = new List<string>();

        foreach (var batch in SplitClientBatches(sqlText, databaseType))
        {
            if (!ContainsExecutableTokens(batch, databaseType))
                continue;

            if (databaseType == DatabaseType.Oracle && IsOracleRoutineBatch(batch))
            {
                statements.Add(batch.Trim());
                continue;
            }

            if (databaseType == DatabaseType.Oracle && IsOracleAnonymousBlockBatch(batch))
            {
                statements.Add(batch.Trim());
                continue;
            }

            statements.AddRange(SplitStandardStatements(batch, databaseType));
        }

        return statements;
    }

    private static IEnumerable<string> SplitClientBatches(string sqlText, DatabaseType databaseType)
    {
        var batches = new List<string>();
        var current = new StringBuilder();
        var lines = sqlText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        foreach (var line in lines)
        {
            if (databaseType == DatabaseType.Oracle && string.Equals(line.Trim(), "/", StringComparison.Ordinal))
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

    private static IEnumerable<string> SplitStandardStatements(string sqlText, DatabaseType databaseType)
    {
        var lexer = ProviderSqlLexerFactory.Create(databaseType, sqlText);
        var tokens = new CommonTokenStream(lexer);
        tokens.Fill();

        var statements = new List<string>();
        var allTokens = tokens.GetTokens();

        int? startIndex = null;
        int? endIndex = null;
        var blockLevel = 0;

        for (var tokenIndex = 0; tokenIndex < allTokens.Count; tokenIndex++)
        {
            var token = allTokens[tokenIndex];
            if (token.Type == TokenConstants.EOF || IsHiddenToken(token))
                continue;

            if (IsToken(token, "begin") && !IsBeginTransaction(allTokens, tokenIndex))
            {
                blockLevel++;
            }
            else if (IsToken(token, "end"))
            {
                if (blockLevel > 0)
                    blockLevel--;
            }

            if ((IsBatchSeparator(token, databaseType) || IsToken(token, ";")) && blockLevel == 0)
            {
                FlushStatement(
                    sqlText,
                    statements,
                    startIndex,
                    endIndex,
                    IsToken(token, ";"),
                    databaseType);
                startIndex = null;
                endIndex = null;
                continue;
            }

            startIndex ??= token.StartIndex;
            endIndex = token.StopIndex;
        }

        FlushStatement(sqlText, statements, startIndex, endIndex, includeDelimiter: false, databaseType);
        return statements;
    }

    private static bool IsBeginTransaction(IList<IToken> tokens, int beginTokenIndex)
    {
        var nextToken = tokens
            .Skip(beginTokenIndex + 1)
            .FirstOrDefault(token => token.Type != TokenConstants.EOF && !IsHiddenToken(token));

        return IsToken(nextToken, "transaction") ||
               IsToken(nextToken, "tran") ||
               IsToken(nextToken, "work");
    }

    private static void FlushStatement(
        string sqlText,
        ICollection<string> statements,
        int? startIndex,
        int? endIndex,
        bool includeDelimiter,
        DatabaseType databaseType)
    {
        if (startIndex is null || endIndex is null)
            return;

        var length = endIndex.Value - startIndex.Value + 1;
        var statement = sqlText.Substring(startIndex.Value, length);
        if (includeDelimiter && ShouldKeepTrailingSemicolon(statement, databaseType))
            statement += ";";

        if (ContainsExecutableTokens(statement, databaseType))
            statements.Add(statement.Trim());
    }

    private static bool ContainsExecutableTokens(string statement, DatabaseType databaseType)
    {
        if (string.IsNullOrWhiteSpace(statement) ||
            (databaseType == DatabaseType.Oracle && IsOracleClientDelimiter(statement)))
        {
            return false;
        }

        var lexer = ProviderSqlLexerFactory.Create(databaseType, statement);
        var tokens = new CommonTokenStream(lexer);
        tokens.Fill();

        foreach (var token in tokens.GetTokens())
        {
            if (token.Type == TokenConstants.EOF || IsHiddenToken(token))
                continue;

            return true;
        }

        return false;
    }

    private static bool IsHiddenToken(IToken token)
    {
        return token.Channel == Lexer.Hidden ||
               token.Channel == TokenConstants.HiddenChannel;
    }

    private static bool IsToken(IToken? token, string text)
    {
        return token is not null &&
               string.Equals(token.Text, text, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBatchSeparator(IToken token, DatabaseType databaseType)
    {
        return databaseType == DatabaseType.SqlServer &&
               IsToken(token, "go");
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

    private static bool ShouldKeepTrailingSemicolon(string statement, DatabaseType databaseType)
    {
        if (databaseType != DatabaseType.Oracle)
            return false;

        var keywords = ReadLeadingKeywords(statement, 1);
        if (keywords.Count == 0)
            return false;

        return string.Equals(keywords[0], "begin", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(keywords[0], "declare", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOracleAnonymousBlockBatch(string statement)
    {
        return ShouldKeepTrailingSemicolon(statement, DatabaseType.Oracle) &&
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
