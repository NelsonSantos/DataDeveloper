using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;

namespace DataDeveloper.Services;

public static class SqlParameterDetector
{
    private static readonly Regex RoutineDeclarationRegex = new(
        @"(?ix)\b(?:(?:create\s+or\s+alter)|create|alter)\s+(?<kind>procedure|function)\b",
        RegexOptions.Compiled);
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

            if (current is '@' or ':')
            {
                if (current == '@' && i + 1 < sql.Length && sql[i + 1] == '@')
                {
                    i += 2;
                    continue;
                }

                if (current == ':' && i + 1 < sql.Length && sql[i + 1] == ':')
                {
                    i += 2;
                    continue;
                }

                var parameter = ReadParameter(sql, i, current);
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

    private static string? ReadParameter(string sql, int startIndex, char prefix)
    {
        if (startIndex + 1 >= sql.Length)
            return null;

        if (prefix == '@' && sql[startIndex + 1] == '@')
            return null;

        var next = sql[startIndex + 1];
        if (!IsIdentifierStart(next))
            return null;

        var builder = new StringBuilder(prefix.ToString());
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
            var declarationMatch = FindNextRoutineDeclaration(lowerSql, searchIndex);
            if (declarationMatch is null)
                break;

            var declarationStart = declarationMatch.Index;
            var declarationHeaderEnd = FindDeclarationHeaderEnd(declarationMatch);
            if (declarationHeaderEnd < 0)
                break;

            var declarationBodyStart = FindDeclarationBodyStart(lowerSql, declarationHeaderEnd);
            if (declarationBodyStart < 0)
                break;

            var declarationEnd = FindRoutineDeclarationEnd(lowerSql, declarationBodyStart);
            if (declarationEnd < 0)
                declarationEnd = lowerSql.Length;

            for (var i = declarationStart; i < declarationEnd; i++)
                builder[i] = ' ';

            searchIndex = declarationEnd;
        }

        return builder.ToString();
    }

    private static Match? FindNextRoutineDeclaration(string sql, int startIndex)
    {
        var match = RoutineDeclarationRegex.Match(sql, startIndex);
        return match.Success ? match : null;
    }

    private static int FindDeclarationHeaderEnd(Match declarationMatch)
    {
        var kindGroup = declarationMatch.Groups["kind"];
        if (!kindGroup.Success)
            return -1;

        return kindGroup.Index + kindGroup.Length;
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

    private static int FindRoutineDeclarationEnd(string sql, int bodyStartIndex)
    {
        var batchSeparatorIndex = FindNextBatchSeparator(sql, bodyStartIndex);
        var bodyEndIndex = FindRoutineBodyEnd(sql, bodyStartIndex);

        if (batchSeparatorIndex >= 0 && bodyEndIndex >= 0)
            return Math.Min(batchSeparatorIndex, bodyEndIndex);

        return batchSeparatorIndex >= 0 ? batchSeparatorIndex : bodyEndIndex;
    }

    private static int FindNextBatchSeparator(string sql, int startIndex)
    {
        var index = startIndex;
        while (index < sql.Length)
        {
            var lineStart = index;
            while (lineStart < sql.Length && (sql[lineStart] == '\r' || sql[lineStart] == '\n'))
                lineStart++;

            if (lineStart >= sql.Length)
                return -1;

            var lineEnd = lineStart;
            while (lineEnd < sql.Length && sql[lineEnd] != '\r' && sql[lineEnd] != '\n')
                lineEnd++;

            var line = sql[lineStart..lineEnd].Trim();
            if (string.Equals(line, "go", StringComparison.OrdinalIgnoreCase))
                return lineStart;

            index = lineEnd + 1;
        }

        return -1;
    }

    private static int FindRoutineBodyEnd(string sql, int bodyStartIndex)
    {
        var index = bodyStartIndex;
        var beginDepth = 0;
        var sawBegin = false;

        while (index < sql.Length)
        {
            if (sql[index] == '\'' || sql[index] == '"')
            {
                index = SkipQuotedString(sql, index, sql[index]);
                continue;
            }

            if (sql[index] == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
            {
                index = SkipSingleLineComment(sql, index);
                continue;
            }

            if (sql[index] == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
            {
                index = SkipMultiLineComment(sql, index);
                continue;
            }

            if (IsKeywordAt(sql, index, "begin"))
            {
                beginDepth++;
                sawBegin = true;
                index += "begin".Length;
                continue;
            }

            if (IsKeywordAt(sql, index, "end"))
            {
                if (beginDepth > 0)
                {
                    beginDepth--;
                    index += "end".Length;
                    if (sawBegin && beginDepth == 0)
                        return ConsumeStatementTerminator(sql, index);
                    continue;
                }
            }

            if (!sawBegin && sql[index] == ';')
                return index + 1;

            index++;
        }

        return sql.Length;
    }

    private static int ConsumeStatementTerminator(string sql, int index)
    {
        while (index < sql.Length && char.IsWhiteSpace(sql[index]))
            index++;

        if (index < sql.Length && sql[index] == ';')
            return index + 1;

        return index;
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
