using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Services;

public static class ResultSetEditabilityAnalyzer
{
    public static ResultSetEditabilityInfo Analyze(
        string statement,
        IReadOnlyCollection<string>? resultColumns = null,
        IReadOnlyCollection<ColumnModel>? tableColumns = null)
    {
        if (string.IsNullOrWhiteSpace(statement))
            return new ResultSetEditabilityInfo(false, null, "Statement is empty.");

        if (!TryParseEditableSelect(statement, out var parsed))
        {
            return new ResultSetEditabilityInfo(
                false,
                null,
                "Only simple single-table 'select *' statements are editable.");
        }

        var projectionAlias = parsed.ProjectionAlias;
        var tableAlias = parsed.TableAlias;
        if (!string.IsNullOrWhiteSpace(projectionAlias) &&
            !string.IsNullOrWhiteSpace(tableAlias) &&
            !string.Equals(NormalizeIdentifier(projectionAlias), NormalizeIdentifier(tableAlias), StringComparison.OrdinalIgnoreCase))
        {
            return new ResultSetEditabilityInfo(
                false,
                null,
                "Only simple single-table 'select *' statements are editable.");
        }

        var tableName = parsed.TableName;

        if (tableColumns is null || resultColumns is null)
            return new ResultSetEditabilityInfo(true, tableName, null);

        var primaryKeyColumns = tableColumns
            .Where(column => column.IsPrimaryKey)
            .Select(column => column.Name)
            .ToList();

        if (primaryKeyColumns.Count == 0)
            return new ResultSetEditabilityInfo(false, tableName, "The target table does not have a primary key.");

        var visibleColumns = new HashSet<string>(resultColumns, StringComparer.OrdinalIgnoreCase);
        var missingKeyColumns = primaryKeyColumns
            .Where(column => !visibleColumns.Contains(column))
            .ToList();

        if (missingKeyColumns.Count > 0)
        {
            return new ResultSetEditabilityInfo(
                false,
                tableName,
                $"Primary key columns are missing from the result: {string.Join(", ", missingKeyColumns)}.");
        }

        return new ResultSetEditabilityInfo(true, tableName, null);
    }

    private static bool TryParseEditableSelect(string statement, out ParsedEditableSelect parsed)
    {
        var tokens = Tokenize(statement);
        var index = 0;
        parsed = default;

        if (!TryConsumeKeyword(tokens, ref index, "select"))
            return false;

        string? projectionAlias = null;
        if (TryConsumeSymbol(tokens, ref index, "*"))
        {
        }
        else
        {
            if (!TryConsumeIdentifier(tokens, ref index, out projectionAlias))
                return false;

            if (!TryConsumeSymbol(tokens, ref index, "."))
                return false;

            if (!TryConsumeSymbol(tokens, ref index, "*"))
                return false;
        }

        if (!TryConsumeKeyword(tokens, ref index, "from"))
            return false;

        if (!TryConsumeQualifiedName(tokens, ref index, out var tableName))
            return false;

        string? tableAlias = null;
        var aliasIndex = index;
        TryConsumeKeyword(tokens, ref aliasIndex, "as");
        if (TryConsumeAlias(tokens, ref aliasIndex, out var parsedAlias))
        {
            tableAlias = parsedAlias;
            index = aliasIndex;
        }

        var foundWhere = false;
        var foundOrderBy = false;
        var parenDepth = 0;

        while (index < tokens.Count)
        {
            if (TryConsumeSymbol(tokens, ref index, "("))
            {
                parenDepth++;
                continue;
            }

            if (TryConsumeSymbol(tokens, ref index, ")"))
            {
                if (parenDepth > 0)
                    parenDepth--;
                continue;
            }

            if (TryConsumeSymbol(tokens, ref index, ";"))
                continue;

            if (parenDepth == 0 && !foundWhere && TryConsumeKeyword(tokens, ref index, "where"))
            {
                foundWhere = true;
                continue;
            }

            if (parenDepth == 0 &&
                !foundOrderBy &&
                TryConsumeKeyword(tokens, ref index, "order") &&
                TryConsumeKeyword(tokens, ref index, "by"))
            {
                foundOrderBy = true;
                continue;
            }

            if (parenDepth == 0 && IsUnsupportedTopLevelToken(tokens, index))
                return false;

            index++;
        }

        parsed = new ParsedEditableSelect(
            NormalizeQualifiedName(tableName),
            projectionAlias,
            tableAlias);
        return true;
    }

    private static string NormalizeQualifiedName(string value)
    {
        var parts = value
            .Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim());

        return string.Join(".", parts);
    }

    private static string NormalizeIdentifier(string value)
    {
        return value.Trim().Trim('[', ']', '`', '"');
    }

    private static bool IsUnsupportedTopLevelToken(IReadOnlyList<SqlToken> tokens, int index)
    {
        if (index < 0 || index >= tokens.Count)
            return false;

        return tokens[index].Kind == SqlTokenKind.Word &&
               tokens[index].UpperText is "JOIN" or "INNER" or "LEFT" or "RIGHT" or "FULL" or "CROSS" or "GROUP" or "HAVING";
    }

    private static bool TryConsumeKeyword(IReadOnlyList<SqlToken> tokens, ref int index, string keyword)
    {
        if (index >= tokens.Count || tokens[index].Kind != SqlTokenKind.Word)
            return false;

        if (!string.Equals(tokens[index].UpperText, keyword, StringComparison.OrdinalIgnoreCase))
            return false;

        index++;
        return true;
    }

    private static bool TryConsumeIdentifier(IReadOnlyList<SqlToken> tokens, ref int index, out string value)
    {
        value = string.Empty;
        if (index >= tokens.Count || !tokens[index].IsIdentifier)
            return false;

        value = tokens[index].Text;
        index++;
        return true;
    }

    private static bool TryConsumeAlias(IReadOnlyList<SqlToken> tokens, ref int index, out string value)
    {
        value = string.Empty;
        if (index >= tokens.Count || !tokens[index].IsIdentifier || IsReservedWord(tokens[index]))
            return false;

        value = tokens[index].Text;
        index++;
        return true;
    }

    private static bool TryConsumeSymbol(IReadOnlyList<SqlToken> tokens, ref int index, string symbol)
    {
        if (index >= tokens.Count || tokens[index].Kind != SqlTokenKind.Symbol)
            return false;

        if (!string.Equals(tokens[index].Text, symbol, StringComparison.Ordinal))
            return false;

        index++;
        return true;
    }

    private static bool TryConsumeQualifiedName(IReadOnlyList<SqlToken> tokens, ref int index, out string value)
    {
        value = string.Empty;
        if (!TryConsumeIdentifier(tokens, ref index, out var first))
            return false;

        value = first;
        var nextIndex = index;
        if (TryConsumeSymbol(tokens, ref nextIndex, ".") && TryConsumeIdentifier(tokens, ref nextIndex, out var second))
        {
            value = $"{first}.{second}";
            index = nextIndex;
        }

        return true;
    }

    private static bool IsReservedWord(SqlToken token)
    {
        return token.Kind == SqlTokenKind.Word &&
               token.UpperText is
                   "WHERE" or "ORDER" or "GROUP" or "HAVING" or "JOIN" or "INNER" or
                   "LEFT" or "RIGHT" or "FULL" or "CROSS" or "ON";
    }

    private static List<SqlToken> Tokenize(string statement)
    {
        var tokens = new List<SqlToken>();
        var index = 0;

        while (index < statement.Length)
        {
            var current = statement[index];

            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (current == '-' && index + 1 < statement.Length && statement[index + 1] == '-')
            {
                index += 2;
                while (index < statement.Length && statement[index] != '\n')
                    index++;

                continue;
            }

            if (current == '/' && index + 1 < statement.Length && statement[index + 1] == '*')
            {
                index += 2;
                while (index + 1 < statement.Length && !(statement[index] == '*' && statement[index + 1] == '/'))
                    index++;

                if (index + 1 < statement.Length)
                    index += 2;

                continue;
            }

            if (current is '[' or '`' or '"')
            {
                var closing = current == '[' ? ']' : current;
                var start = index++;
                while (index < statement.Length && statement[index] != closing)
                    index++;

                if (index < statement.Length)
                    index++;

                tokens.Add(new SqlToken(statement[start..index], SqlTokenKind.Identifier));
                continue;
            }

            if (char.IsLetter(current) || current == '_')
            {
                var start = index++;
                while (index < statement.Length && (char.IsLetterOrDigit(statement[index]) || statement[index] is '_' or '$'))
                    index++;

                tokens.Add(new SqlToken(statement[start..index], SqlTokenKind.Word));
                continue;
            }

            if (char.IsDigit(current))
            {
                var start = index++;
                while (index < statement.Length && char.IsDigit(statement[index]))
                    index++;

                tokens.Add(new SqlToken(statement[start..index], SqlTokenKind.Other));
                continue;
            }

            tokens.Add(new SqlToken(statement[index].ToString(), SqlTokenKind.Symbol));
            index++;
        }

        return tokens;
    }

    private readonly record struct ParsedEditableSelect(
        string TableName,
        string? ProjectionAlias,
        string? TableAlias);

    private enum SqlTokenKind
    {
        Word,
        Identifier,
        Symbol,
        Other
    }

    private readonly record struct SqlToken(string Text, SqlTokenKind Kind)
    {
        public string UpperText => Text.ToUpperInvariant();
        public bool IsIdentifier => Kind is SqlTokenKind.Identifier or SqlTokenKind.Word;
    }
}
