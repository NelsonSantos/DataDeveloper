using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;

namespace DataDeveloper.Services;

public static class SqlParameterDetector
{
    private static readonly Regex DeclaredParameterRegex = new(
        @"\bdeclare\s+(?<parameter>@[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<string> ExtractParameters(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return [];

        sql = RemoveRoutineDeclarationParameters(sql);
        var declaredParameters = CollectDeclaredParameters(sql);

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var i = 0;

        while (i < sql.Length)
        {
            var current = sql[i];

            if (current == '\'' || current == '"')
            {
                i = SkipQuotedString(sql, i, current);
                continue;
            }

            if (current == '[')
            {
                i = SkipBracketIdentifier(sql, i);
                continue;
            }

            if (current == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                i = SkipSingleLineComment(sql, i);
                continue;
            }

            if (current == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                i = SkipMultiLineComment(sql, i);
                continue;
            }

            if (current == '@')
            {
                if (i + 1 < sql.Length && sql[i + 1] == '@')
                {
                    i += 2;
                    continue;
                }

                var parameter = ReadParameter(sql, i);
                if (parameter is not null && !declaredParameters.Contains(parameter) && seen.Add(parameter))
                    result.Add(parameter);

                i += parameter?.Length ?? 1;
                continue;
            }

            i++;
        }

        return result;
    }

    private static HashSet<string> CollectDeclaredParameters(string sql)
    {
        var sanitized = SanitizeForParameterDetection(sql);
        var declaredParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in DeclaredParameterRegex.Matches(sanitized))
        {
            var parameter = match.Groups["parameter"].Value;
            if (!string.IsNullOrWhiteSpace(parameter))
                declaredParameters.Add(parameter);
        }

        return declaredParameters;
    }

    private static string SanitizeForParameterDetection(string sql)
    {
        var buffer = sql.ToCharArray();
        var i = 0;

        while (i < buffer.Length)
        {
            if (buffer[i] == '\'' || buffer[i] == '"')
            {
                i = BlankQuotedString(buffer, i, buffer[i]);
                continue;
            }

            if (buffer[i] == '[')
            {
                i = BlankBracketIdentifier(buffer, i);
                continue;
            }

            if (buffer[i] == '-' && i + 1 < buffer.Length && buffer[i + 1] == '-')
            {
                i = BlankSingleLineComment(buffer, i);
                continue;
            }

            if (buffer[i] == '/' && i + 1 < buffer.Length && buffer[i + 1] == '*')
            {
                i = BlankMultiLineComment(buffer, i);
                continue;
            }

            i++;
        }

        return new string(buffer);
    }

    private static string? ReadParameter(string sql, int startIndex)
    {
        if (startIndex + 1 >= sql.Length)
            return null;

        if (sql[startIndex + 1] == '@')
            return null;

        var next = sql[startIndex + 1];
        if (!IsIdentifierStart(next))
            return null;

        var builder = new StringBuilder("@");
        builder.Append(next);

        var index = startIndex + 2;
        while (index < sql.Length && IsIdentifierPart(sql[index]))
        {
            builder.Append(sql[index]);
            index++;
        }

        return builder.ToString();
    }

    private static int SkipQuotedString(string sql, int startIndex, char quote)
    {
        var index = startIndex + 1;
        while (index < sql.Length)
        {
            if (sql[index] == quote)
            {
                if (index + 1 < sql.Length && sql[index + 1] == quote)
                {
                    index += 2;
                    continue;
                }

                return index + 1;
            }

            index++;
        }

        return sql.Length;
    }

    private static int BlankQuotedString(char[] sql, int startIndex, char quote)
    {
        var index = startIndex;
        sql[index++] = ' ';

        while (index < sql.Length)
        {
            var current = sql[index];
            sql[index] = ' ';

            if (current == quote)
            {
                if (index + 1 < sql.Length && sql[index + 1] == quote)
                {
                    sql[index + 1] = ' ';
                    index += 2;
                    continue;
                }

                return index + 1;
            }

            index++;
        }

        return sql.Length;
    }

    private static int SkipBracketIdentifier(string sql, int startIndex)
    {
        var index = startIndex + 1;
        while (index < sql.Length)
        {
            if (sql[index] == ']')
                return index + 1;

            index++;
        }

        return sql.Length;
    }

    private static int BlankBracketIdentifier(char[] sql, int startIndex)
    {
        var index = startIndex;
        sql[index++] = ' ';

        while (index < sql.Length)
        {
            var current = sql[index];
            sql[index] = ' ';
            if (current == ']')
                return index + 1;

            index++;
        }

        return sql.Length;
    }

    private static int SkipSingleLineComment(string sql, int startIndex)
    {
        var index = startIndex + 2;
        while (index < sql.Length && sql[index] != '\n')
            index++;

        return index;
    }

    private static int BlankSingleLineComment(char[] sql, int startIndex)
    {
        var index = startIndex;
        while (index < sql.Length && sql[index] != '\n')
        {
            sql[index] = ' ';
            index++;
        }

        return index;
    }

    private static int SkipMultiLineComment(string sql, int startIndex)
    {
        var index = startIndex + 2;
        while (index + 1 < sql.Length)
        {
            if (sql[index] == '*' && sql[index + 1] == '/')
                return index + 2;

            index++;
        }

        return sql.Length;
    }

    private static int BlankMultiLineComment(char[] sql, int startIndex)
    {
        var index = startIndex;
        while (index + 1 < sql.Length)
        {
            var current = sql[index];
            sql[index] = ' ';

            if (current == '*' && sql[index + 1] == '/')
            {
                sql[index + 1] = ' ';
                return index + 2;
            }

            index++;
        }

        if (index < sql.Length)
            sql[index] = ' ';

        return sql.Length;
    }

    private static bool IsIdentifierStart(char value) =>
        char.IsLetter(value) || value == '_';

    private static bool IsIdentifierPart(char value) =>
        char.IsLetterOrDigit(value) || value == '_';

    private static string RemoveRoutineDeclarationParameters(string sql)
    {
        var lowerSql = sql.ToLowerInvariant();
        var builder = new StringBuilder(sql);
        var searchIndex = 0;

        while (searchIndex < lowerSql.Length)
        {
            var declarationStart = FindNextRoutineDeclaration(lowerSql, searchIndex);
            if (declarationStart < 0)
                break;

            var declarationHeaderEnd = FindDeclarationHeaderEnd(lowerSql, declarationStart);
            if (declarationHeaderEnd < 0)
                break;

            var declarationBodyStart = FindDeclarationBodyStart(lowerSql, declarationHeaderEnd);
            if (declarationBodyStart < 0)
                break;

            for (var i = declarationHeaderEnd; i < declarationBodyStart; i++)
                builder[i] = ' ';

            searchIndex = declarationBodyStart;
        }

        return builder.ToString();
    }

    private static int FindNextRoutineDeclaration(string sql, int startIndex)
    {
        var candidates = new[]
        {
            "create procedure",
            "alter procedure",
            "create function",
            "alter function",
            "create or alter procedure",
            "create or alter function"
        };

        var next = -1;
        foreach (var candidate in candidates)
        {
            var index = sql.IndexOf(candidate, startIndex, StringComparison.Ordinal);
            if (index >= 0 && (next < 0 || index < next))
                next = index;
        }

        return next;
    }

    private static int FindDeclarationHeaderEnd(string sql, int declarationStart)
    {
        var procedureIndex = sql.IndexOf("procedure", declarationStart, StringComparison.Ordinal);
        var functionIndex = sql.IndexOf("function", declarationStart, StringComparison.Ordinal);
        var keywordIndex = procedureIndex >= 0 && (functionIndex < 0 || procedureIndex < functionIndex)
            ? procedureIndex
            : functionIndex;

        if (keywordIndex < 0)
            return -1;

        return keywordIndex + (procedureIndex == keywordIndex ? "procedure".Length : "function".Length);
    }

    private static int FindDeclarationBodyStart(string sql, int startIndex)
    {
        var i = startIndex;
        while (i < sql.Length)
        {
            if (sql[i] == '\'' || sql[i] == '"')
            {
                i = SkipQuotedString(sql, i, sql[i]);
                continue;
            }

            if (sql[i] == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                i = SkipSingleLineComment(sql, i);
                continue;
            }

            if (sql[i] == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                i = SkipMultiLineComment(sql, i);
                continue;
            }

            if (IsKeywordAt(sql, i, "as") || IsKeywordAt(sql, i, "begin"))
                return i;

            i++;
        }

        return -1;
    }

    private static bool IsKeywordAt(string sql, int index, string keyword)
    {
        if (index + keyword.Length > sql.Length)
            return false;

        if (!sql.AsSpan(index, keyword.Length).SequenceEqual(keyword))
            return false;

        var beforeIsIdentifier = index > 0 && IsIdentifierPart(sql[index - 1]);
        var afterIndex = index + keyword.Length;
        var afterIsIdentifier = afterIndex < sql.Length && IsIdentifierPart(sql[afterIndex]);
        return !beforeIsIdentifier && !afterIsIdentifier;
    }
}
