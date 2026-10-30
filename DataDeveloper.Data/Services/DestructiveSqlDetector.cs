using System.Text;
using DataDeveloper.Data.Enums;

namespace DataDeveloper.Data.Services;

public enum DestructiveSqlKind
{
    Drop,
    Truncate,
    DeleteWithoutWhere,
    UpdateWithoutWhere,
    AlwaysTrueWhere,
    AlterDrop
}

/// <summary>A statement that can lose data in a way the user may not expect, with the reason it was flagged.</summary>
public sealed record DestructiveStatement(DestructiveSqlKind Kind, string Statement)
{
    public string Description => Kind switch
    {
        DestructiveSqlKind.Drop => "DROP permanently removes the object and its data.",
        DestructiveSqlKind.Truncate => "TRUNCATE removes every row of the table at once.",
        DestructiveSqlKind.DeleteWithoutWhere => "DELETE without WHERE is not limited to specific rows.",
        DestructiveSqlKind.UpdateWithoutWhere => "UPDATE without WHERE changes every row.",
        DestructiveSqlKind.AlwaysTrueWhere => "The WHERE condition is always true, so every row is affected.",
        _ => "ALTER TABLE ... DROP removes a column or a constraint."
    };
}

/// <summary>
/// Finds the statements of a script that can lose data: DROP, TRUNCATE, DELETE and UPDATE that are not restricted by a
/// WHERE (or whose WHERE is always true) and ALTER TABLE ... DROP. It reads the text itself, with no model involved, and
/// skips comments, string literals and quoted identifiers per dialect, so a keyword inside them never counts.
/// It does not see what a statement does at run time (how many rows a WHERE matches, dynamic SQL, the body of a routine).
/// </summary>
public static class DestructiveSqlDetector
{
    public static IReadOnlyList<DestructiveStatement> Find(string sqlText, DatabaseType databaseType)
    {
        if (string.IsNullOrWhiteSpace(sqlText))
            return [];

        var found = new List<DestructiveStatement>();
        foreach (var statement in StatementSplitter.SplitStatements(sqlText, databaseType))
        {
            var kind = Classify(Tokenize(statement, databaseType));
            if (kind is { } risk)
                found.Add(new DestructiveStatement(risk, statement));
        }

        return found;
    }

    private static DestructiveSqlKind? Classify(List<Token> tokens)
    {
        var commandIndex = FindCommandIndex(tokens);
        if (commandIndex < 0)
            return null;

        switch (tokens[commandIndex].Word)
        {
            case "DROP":
                return DestructiveSqlKind.Drop;
            case "TRUNCATE":
                return DestructiveSqlKind.Truncate;
            case "DELETE":
                return ClassifyRowChange(tokens, commandIndex, DestructiveSqlKind.DeleteWithoutWhere);
            case "UPDATE":
                return ClassifyRowChange(tokens, commandIndex, DestructiveSqlKind.UpdateWithoutWhere);
            case "ALTER":
                return IsAlterTableDrop(tokens, commandIndex) ? DestructiveSqlKind.AlterDrop : null;
            default:
                return null;
        }
    }

    // "WITH cte AS (...) DELETE ..." starts with WITH; the command is the first statement keyword outside parentheses.
    private static int FindCommandIndex(List<Token> tokens)
    {
        if (tokens.Count == 0)
            return -1;

        if (tokens[0].Word != "WITH")
            return 0;

        for (var index = 1; index < tokens.Count; index++)
        {
            if (tokens[index].Depth == 0 && tokens[index].Word is "SELECT" or "INSERT" or "UPDATE" or "DELETE" or "MERGE")
                return index;
        }

        return -1;
    }

    private static DestructiveSqlKind? ClassifyRowChange(List<Token> tokens, int commandIndex, DestructiveSqlKind withoutWhere)
    {
        var whereIndex = -1;
        for (var index = commandIndex + 1; index < tokens.Count; index++)
        {
            if (tokens[index].Depth == 0 && tokens[index].Word == "WHERE")
            {
                whereIndex = index;
                break;
            }
        }

        if (whereIndex < 0)
            return withoutWhere;

        return HasAlwaysTrueCondition(tokens, whereIndex) ? DestructiveSqlKind.AlwaysTrueWhere : null;
    }

    // "WHERE 1 = 1", "WHERE TRUE" and "... OR 1 = 1" when it closes the condition.
    private static bool HasAlwaysTrueCondition(List<Token> tokens, int whereIndex)
    {
        var end = tokens.Count;
        while (end > whereIndex + 1 && tokens[end - 1].IsSymbol(';'))
            end--;

        for (var start = whereIndex + 1; start < end; start++)
        {
            if (tokens[start].Depth != 0)
                continue;

            var precededByWhereOrOr = start == whereIndex + 1 || tokens[start - 1].Word == "OR";
            if (!precededByWhereOrOr)
                continue;

            var isTrue = tokens[start].Word == "TRUE";
            var isSameComparison = start + 2 < end &&
                                   tokens[start].IsValue && tokens[start + 2].IsValue &&
                                   tokens[start + 1].IsSymbol('=') &&
                                   tokens[start].Text == tokens[start + 2].Text;

            var length = isTrue ? 1 : isSameComparison ? 3 : 0;
            if (length > 0 && (start + length == end || tokens[start + length].Word == "OR"))
                return true;
        }

        return false;
    }

    private static bool IsAlterTableDrop(List<Token> tokens, int commandIndex)
    {
        if (commandIndex + 1 >= tokens.Count || tokens[commandIndex + 1].Word != "TABLE")
            return false;

        for (var index = commandIndex + 2; index < tokens.Count; index++)
        {
            if (tokens[index].Depth != 0 || tokens[index].Word != "DROP")
                continue;

            // "ALTER COLUMN c DROP DEFAULT / NOT NULL / IDENTITY" changes a property, it does not remove anything.
            var next = index + 1 < tokens.Count ? tokens[index + 1].Word : null;
            if (next is "DEFAULT" or "NOT" or "IDENTITY" or "EXPRESSION")
                continue;

            return true;
        }

        return false;
    }

    private enum TokenKind
    {
        Word,
        Quoted,
        Literal,
        Number,
        Symbol
    }

    private readonly record struct Token(TokenKind Kind, string Text, int Depth)
    {
        public string? Word => Kind == TokenKind.Word ? Text.ToUpperInvariant() : null;
        public bool IsValue => Kind is TokenKind.Number or TokenKind.Literal;
        public bool IsSymbol(char symbol) => Kind == TokenKind.Symbol && Text.Length == 1 && Text[0] == symbol;
    }

    private static List<Token> Tokenize(string text, DatabaseType databaseType)
    {
        var tokens = new List<Token>();
        var depth = 0;
        var index = 0;

        while (index < text.Length)
        {
            var current = text[index];

            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (current == '-' && Next(text, index) == '-' && (databaseType != DatabaseType.MySql || IsSpaceOrEnd(text, index + 2)))
            {
                index = SkipLine(text, index);
                continue;
            }

            if (current == '#' && databaseType == DatabaseType.MySql)
            {
                index = SkipLine(text, index);
                continue;
            }

            if (current == '/' && Next(text, index) == '*')
            {
                var close = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
                index = close < 0 ? text.Length : close + 2;
                continue;
            }

            if (current == '\'')
            {
                var backslashEscapes = databaseType == DatabaseType.MySql;
                index = ReadString(text, index, backslashEscapes, tokens, depth);
                continue;
            }

            if (current == '"' || current == '`' || (current == '[' && databaseType is DatabaseType.SqlServer or DatabaseType.SqLite))
            {
                var closing = current == '[' ? ']' : current;
                index = ReadQuotedIdentifier(text, index, closing, tokens, depth);
                continue;
            }

            if (current == '$' && databaseType == DatabaseType.PostgresSql && TryReadDollarQuoted(text, index, tokens, depth, out var afterDollar))
            {
                index = afterDollar;
                continue;
            }

            if (char.IsLetter(current) || current == '_')
            {
                var start = index++;
                while (index < text.Length && IsIdentifierChar(text[index]))
                    index++;

                var word = text[start..index];
                if (index < text.Length && text[index] == '\'' && word.Length == 1)
                {
                    // Oracle q'[...]' and PostgreSQL E'...' (backslash escapes) are literals with their own rules.
                    if (databaseType == DatabaseType.Oracle && char.ToUpperInvariant(word[0]) == 'Q' &&
                        TryReadOracleAlternativeQuote(text, index, tokens, depth, out var afterQuote))
                    {
                        index = afterQuote;
                        continue;
                    }

                    if (databaseType == DatabaseType.PostgresSql && char.ToUpperInvariant(word[0]) == 'E')
                    {
                        index = ReadString(text, index, backslashEscapes: true, tokens, depth);
                        continue;
                    }
                }

                tokens.Add(new Token(TokenKind.Word, word, depth));
                continue;
            }

            if (char.IsDigit(current))
            {
                var start = index++;
                while (index < text.Length && (char.IsDigit(text[index]) || text[index] == '.'))
                    index++;

                tokens.Add(new Token(TokenKind.Number, text[start..index], depth));
                continue;
            }

            if (current == '(')
            {
                tokens.Add(new Token(TokenKind.Symbol, "(", depth));
                depth++;
            }
            else if (current == ')')
            {
                depth = Math.Max(0, depth - 1);
                tokens.Add(new Token(TokenKind.Symbol, ")", depth));
            }
            else
            {
                tokens.Add(new Token(TokenKind.Symbol, current.ToString(), depth));
            }

            index++;
        }

        return tokens;
    }

    private static char? Next(string text, int index) => index + 1 < text.Length ? text[index + 1] : null;

    private static bool IsSpaceOrEnd(string text, int index) => index >= text.Length || char.IsWhiteSpace(text[index]);

    private static bool IsIdentifierChar(char value) => char.IsLetterOrDigit(value) || value is '_' or '$' or '#';

    private static int SkipLine(string text, int index)
    {
        var end = text.IndexOf('\n', index);
        return end < 0 ? text.Length : end + 1;
    }

    private static int ReadString(string text, int quoteIndex, bool backslashEscapes, List<Token> tokens, int depth)
    {
        var builder = new StringBuilder();
        var index = quoteIndex + 1;
        while (index < text.Length)
        {
            var current = text[index];
            if (backslashEscapes && current == '\\' && index + 1 < text.Length)
            {
                builder.Append(text[index + 1]);
                index += 2;
                continue;
            }

            if (current == '\'')
            {
                if (Next(text, index) == '\'')
                {
                    builder.Append('\'');
                    index += 2;
                    continue;
                }

                index++;
                break;
            }

            builder.Append(current);
            index++;
        }

        tokens.Add(new Token(TokenKind.Literal, builder.ToString(), depth));
        return index;
    }

    private static int ReadQuotedIdentifier(string text, int openIndex, char closing, List<Token> tokens, int depth)
    {
        var index = openIndex + 1;
        while (index < text.Length && text[index] != closing)
            index++;

        tokens.Add(new Token(TokenKind.Quoted, text[(openIndex + 1)..Math.Min(index, text.Length)], depth));
        return Math.Min(index + 1, text.Length);
    }

    private static bool TryReadDollarQuoted(string text, int start, List<Token> tokens, int depth, out int next)
    {
        next = start;
        var tagEnd = start + 1;
        while (tagEnd < text.Length && (char.IsLetterOrDigit(text[tagEnd]) || text[tagEnd] == '_'))
            tagEnd++;

        if (tagEnd >= text.Length || text[tagEnd] != '$' || (tagEnd > start + 1 && char.IsDigit(text[start + 1])))
            return false;

        var tag = text[start..(tagEnd + 1)];
        var close = text.IndexOf(tag, tagEnd + 1, StringComparison.Ordinal);
        var bodyEnd = close < 0 ? text.Length : close;
        tokens.Add(new Token(TokenKind.Literal, text[(tagEnd + 1)..bodyEnd], depth));
        next = close < 0 ? text.Length : close + tag.Length;
        return true;
    }

    private static bool TryReadOracleAlternativeQuote(string text, int quoteIndex, List<Token> tokens, int depth, out int next)
    {
        next = quoteIndex;
        if (quoteIndex + 1 >= text.Length || char.IsWhiteSpace(text[quoteIndex + 1]))
            return false;

        var opening = text[quoteIndex + 1];
        var closing = opening switch { '[' => ']', '{' => '}', '(' => ')', '<' => '>', _ => opening };
        var close = text.IndexOf($"{closing}'", quoteIndex + 2, StringComparison.Ordinal);
        var bodyEnd = close < 0 ? text.Length : close;
        tokens.Add(new Token(TokenKind.Literal, text[(quoteIndex + 2)..bodyEnd], depth));
        next = close < 0 ? text.Length : close + 2;
        return true;
    }
}
