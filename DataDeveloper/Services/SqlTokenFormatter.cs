using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Services;

namespace DataDeveloper.Services;

public static class SqlTokenFormatter
{
    private static readonly HashSet<string> MajorClauses = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "GROUP BY", "ORDER BY", "HAVING",
        "INSERT INTO", "UPDATE", "DELETE FROM", "VALUES", "SET",
        "JOIN", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN",
        "CROSS JOIN", "LEFT OUTER JOIN", "RIGHT OUTER JOIN", "FULL OUTER JOIN",
        "ON", "UNION", "UNION ALL", "LIMIT", "OFFSET"
    };

    private static readonly HashSet<string> ListClauses = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "SET", "VALUES"
    };

    private static readonly HashSet<string> BreakLogicalOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "AND", "OR"
    };

    private static readonly HashSet<string> Operators = new(StringComparer.OrdinalIgnoreCase)
    {
        "=", "<", ">", "<=", ">=", "<>", "!=", "+", "-", "*", "/", "%", "||", ":="
    };

    public static string Format(string sql, DatabaseType databaseType, string indentation)
    {
        var tokens = TryTokenize(sql, databaseType);
        if (tokens.Count == 0)
            return sql.Trim();

        var builder = new StringBuilder(sql.Length + 64);
        var lineStart = true;
        var pendingSpace = false;
        var indentLevel = 0;
        var currentClause = string.Empty;
        var listClauseIndent = -1;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind == SqlFormatTokenKind.Comment)
            {
                EnsureLineStart(builder, ref lineStart, ref pendingSpace);
                WriteIndent(builder, indentLevel, indentation);
                builder.Append(token.Text.Trim());
                AppendNewLine(builder, ref lineStart, ref pendingSpace);
                continue;
            }

            if (TryReadClause(tokens, i, out var clauseText, out var consumed))
            {
                if (builder.Length > 0)
                    AppendNewLine(builder, ref lineStart, ref pendingSpace);

                WriteIndent(builder, indentLevel, indentation);
                builder.Append(clauseText);
                currentClause = clauseText;

                if (ListClauses.Contains(clauseText))
                {
                    listClauseIndent = indentLevel + 1;
                    AppendNewLine(builder, ref lineStart, ref pendingSpace);
                }
                else
                {
                    listClauseIndent = -1;
                    pendingSpace = true;
                    lineStart = false;
                }

                i += consumed - 1;
                continue;
            }

            var upper = token.UpperText;

            if (BreakLogicalOperators.Contains(upper) &&
                (currentClause.Equals("WHERE", StringComparison.OrdinalIgnoreCase) ||
                 currentClause.Equals("ON", StringComparison.OrdinalIgnoreCase) ||
                 currentClause.Equals("HAVING", StringComparison.OrdinalIgnoreCase)))
            {
                AppendNewLine(builder, ref lineStart, ref pendingSpace);
                WriteIndent(builder, indentLevel + 1, indentation);
                builder.Append(token.Text);
                pendingSpace = true;
                lineStart = false;
                continue;
            }

            if (token.Text == ",")
            {
                builder.Append(',');
                if (listClauseIndent >= 0)
                {
                    AppendNewLine(builder, ref lineStart, ref pendingSpace);
                }
                else
                {
                    pendingSpace = true;
                }

                continue;
            }

            if (token.Text == ".")
            {
                builder.Append('.');
                pendingSpace = false;
                lineStart = false;
                continue;
            }

            if (token.Text == "(")
            {
                if (pendingSpace && !lineStart)
                    builder.Append(' ');

                builder.Append('(');
                pendingSpace = false;
                lineStart = false;
                indentLevel++;
                continue;
            }

            if (token.Text == ")")
            {
                indentLevel = Math.Max(0, indentLevel - 1);
                if (lineStart)
                    WriteIndent(builder, indentLevel, indentation);

                builder.Append(')');
                pendingSpace = true;
                lineStart = false;
                continue;
            }

            if (token.Text == ";")
            {
                builder.Append(';');
                AppendNewLine(builder, ref lineStart, ref pendingSpace);
                currentClause = string.Empty;
                listClauseIndent = -1;
                continue;
            }

            if (Operators.Contains(token.Text))
            {
                if (!lineStart && builder.Length > 0 && builder[^1] != ' ')
                    builder.Append(' ');

                builder.Append(token.Text);
                builder.Append(' ');
                pendingSpace = false;
                lineStart = false;
                continue;
            }

            if (lineStart)
            {
                WriteIndent(builder, listClauseIndent >= 0 ? listClauseIndent : indentLevel, indentation);
                lineStart = false;
            }
            else if (pendingSpace)
            {
                builder.Append(' ');
            }

            builder.Append(token.Text);
            pendingSpace = NeedsSpaceAfter(token.Text);
        }

        return NormalizeOutput(builder.ToString());
    }

    private static List<SqlFormatToken> TryTokenize(string sql, DatabaseType databaseType)
    {
        Lexer lexer;
        try
        {
            lexer = ProviderSqlLexerFactory.Create(databaseType, sql);
        }
        catch (NotSupportedException)
        {
            return TokenizeFallback(sql);
        }

        var tokens = new List<SqlFormatToken>();
        while (true)
        {
            var token = lexer.NextToken();
            if (token.Type == TokenConstants.EOF)
                break;

            if (token.Channel == Lexer.Hidden)
            {
                if (IsComment(token.Text))
                    tokens.Add(new SqlFormatToken(token.Text, SqlFormatTokenKind.Comment));

                continue;
            }

            tokens.Add(new SqlFormatToken(token.Text, SqlFormatTokenKind.Code));
        }

        return tokens;
    }

    private static List<SqlFormatToken> TokenizeFallback(string sql)
    {
        return sql.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => new SqlFormatToken(part, SqlFormatTokenKind.Code))
            .ToList();
    }

    private static bool TryReadClause(IReadOnlyList<SqlFormatToken> tokens, int index, out string clauseText, out int consumed)
    {
        clauseText = tokens[index].Text;
        consumed = 1;
        if (tokens[index].Kind != SqlFormatTokenKind.Code)
            return false;

        var single = tokens[index].UpperText;
        if (MajorClauses.Contains(single))
        {
            clauseText = tokens[index].Text;
            return true;
        }

        if (index + 1 >= tokens.Count || tokens[index + 1].Kind != SqlFormatTokenKind.Code)
            return false;

        var combined = $"{single} {tokens[index + 1].UpperText}";
        if (MajorClauses.Contains(combined))
        {
            clauseText = $"{tokens[index].Text} {tokens[index + 1].Text}";
            consumed = 2;
            return true;
        }

        if (index + 2 >= tokens.Count || tokens[index + 2].Kind != SqlFormatTokenKind.Code)
            return false;

        var triple = $"{combined} {tokens[index + 2].UpperText}";
        if (!MajorClauses.Contains(triple))
            return false;

        clauseText = $"{tokens[index].Text} {tokens[index + 1].Text} {tokens[index + 2].Text}";
        consumed = 3;
        return true;
    }

    private static bool IsComment(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.TrimStart();
        return trimmed.StartsWith("--", StringComparison.Ordinal) ||
               trimmed.StartsWith("/*", StringComparison.Ordinal) ||
               trimmed.StartsWith("#", StringComparison.Ordinal);
    }

    private static bool NeedsSpaceAfter(string token)
    {
        return token is not "(" and not "." and not "::";
    }

    private static void EnsureLineStart(StringBuilder builder, ref bool lineStart, ref bool pendingSpace)
    {
        if (!lineStart)
            AppendNewLine(builder, ref lineStart, ref pendingSpace);
    }

    private static void AppendNewLine(StringBuilder builder, ref bool lineStart, ref bool pendingSpace)
    {
        if (builder.Length == 0 || builder[^1] == '\n')
        {
            lineStart = true;
            pendingSpace = false;
            return;
        }

        builder.Append('\n');
        lineStart = true;
        pendingSpace = false;
    }

    private static void WriteIndent(StringBuilder builder, int indentLevel, string indentation)
    {
        for (var i = 0; i < indentLevel; i++)
            builder.Append(indentation);
    }

    private static string NormalizeOutput(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var builder = new StringBuilder(text.Length);
        var previousBlank = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            var isBlank = line.Length == 0;
            if (isBlank && previousBlank)
                continue;

            if (builder.Length > 0)
                builder.Append('\n');

            builder.Append(line);
            previousBlank = isBlank;
        }

        return builder.ToString().Trim();
    }

    private enum SqlFormatTokenKind
    {
        Code,
        Comment
    }

    private readonly record struct SqlFormatToken(string Text, SqlFormatTokenKind Kind)
    {
        public string UpperText { get; } = Text.ToUpperInvariant();
    }
}
