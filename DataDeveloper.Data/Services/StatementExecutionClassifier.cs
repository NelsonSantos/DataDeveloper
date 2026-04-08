namespace DataDeveloper.Data.Services;

public static class StatementExecutionClassifier
{
    public static bool RequiresMaterialization(string statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
            return false;

        var tokens = Tokenize(statement);
        if (tokens.Count == 0)
            return false;

        return IsRoutineInvocation(tokens, 0) ||
               (IsKeyword(tokens, 0, "begin") && ContainsStandaloneExecOrCall(tokens));
    }

    public static bool RequiresSchemaRefresh(string statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
            return false;

        var tokens = Tokenize(statement);
        if (tokens.Count == 0)
            return false;

        return tokens[0].UpperText is "CREATE" or "ALTER" or "DROP" or "TRUNCATE" or "RENAME";
    }

    public static SchemaRefreshTarget? ParseSchemaRefreshTarget(string statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
            return null;

        var tokens = Tokenize(statement);
        if (tokens.Count < 2)
            return null;

        var action = tokens[0].Text;
        var objectKeyword = tokens[1].Text;

        if (action.Equals("truncate", System.StringComparison.OrdinalIgnoreCase) && objectKeyword.Equals("table", System.StringComparison.OrdinalIgnoreCase))
        {
            return BuildTarget(tokens, 2, SchemaRefreshAction.Alter, SchemaObjectType.Table);
        }

        if (action.Equals("rename", System.StringComparison.OrdinalIgnoreCase))
            return new SchemaRefreshTarget(SchemaRefreshAction.Unknown, SchemaObjectType.Unknown, null);

        if (!TryMapAction(action, out var refreshAction) || !TryMapObjectType(objectKeyword, out var objectType))
            return null;

        return BuildTarget(tokens, 2, refreshAction, objectType);
    }

    private static bool IsRoutineInvocation(IReadOnlyList<SqlToken> tokens, int index)
    {
        return IsKeyword(tokens, index, "exec") ||
               IsKeyword(tokens, index, "execute") ||
               IsKeyword(tokens, index, "call");
    }

    private static bool IsKeyword(IReadOnlyList<SqlToken> tokens, int index, string keyword)
    {
        return index >= 0 &&
               index < tokens.Count &&
               tokens[index].Kind == SqlTokenKind.Word &&
               string.Equals(tokens[index].UpperText, keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static List<SqlToken> Tokenize(string statement)
    {
        var tokens = new List<SqlToken>();
        var index = 0;

        while (index < statement.Length)
        {
            while (index < statement.Length && char.IsWhiteSpace(statement[index]))
                index++;

            if (index >= statement.Length)
                break;

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

            if (statement[index] is '[' or '`' or '"')
            {
                var closing = statement[index] == '[' ? ']' : statement[index];
                var start = index++;
                while (index < statement.Length && statement[index] != closing)
                    index++;

                if (index < statement.Length)
                    index++;

                tokens.Add(new SqlToken(statement[start..index], SqlTokenKind.Identifier));
                continue;
            }

            if (char.IsLetter(statement[index]) || statement[index] == '_')
            {
                var start = index++;
                while (index < statement.Length && IsIdentifierChar(statement[index]))
                    index++;

                tokens.Add(new SqlToken(statement[start..index], SqlTokenKind.Word));
                continue;
            }

            if (char.IsDigit(statement[index]))
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

    private static bool ContainsStandaloneExecOrCall(IReadOnlyList<SqlToken> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (IsRoutineInvocation(tokens, i))
                return true;
        }

        return false;
    }

    private static bool IsIdentifierChar(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_' || value == '@' || value == '$';
    }

    private static bool TryMapAction(string value, out SchemaRefreshAction action)
    {
        if (value.Equals("create", System.StringComparison.OrdinalIgnoreCase))
        {
            action = SchemaRefreshAction.Create;
            return true;
        }

        if (value.Equals("alter", System.StringComparison.OrdinalIgnoreCase))
        {
            action = SchemaRefreshAction.Alter;
            return true;
        }

        if (value.Equals("drop", System.StringComparison.OrdinalIgnoreCase))
        {
            action = SchemaRefreshAction.Drop;
            return true;
        }

        action = SchemaRefreshAction.Unknown;
        return false;
    }

    private static bool TryMapObjectType(string value, out SchemaObjectType objectType)
    {
        if (value.Equals("table", System.StringComparison.OrdinalIgnoreCase))
        {
            objectType = SchemaObjectType.Table;
            return true;
        }

        if (value.Equals("view", System.StringComparison.OrdinalIgnoreCase))
        {
            objectType = SchemaObjectType.View;
            return true;
        }

        if (value.Equals("procedure", System.StringComparison.OrdinalIgnoreCase) ||
            value.Equals("proc", System.StringComparison.OrdinalIgnoreCase))
        {
            objectType = SchemaObjectType.Procedure;
            return true;
        }

        if (value.Equals("function", System.StringComparison.OrdinalIgnoreCase))
        {
            objectType = SchemaObjectType.Function;
            return true;
        }

        objectType = SchemaObjectType.Unknown;
        return false;
    }

    private static SchemaRefreshTarget? BuildTarget(IReadOnlyList<SqlToken> tokens, int objectNameIndex, SchemaRefreshAction action, SchemaObjectType objectType)
    {
        if (tokens.Count <= objectNameIndex)
            return new SchemaRefreshTarget(action, objectType, null);

        var objectName = tokens[objectNameIndex].Text
            .Trim()
            .TrimEnd(';', ',')
            .Trim('(', ')');

        if (tokens.Count > objectNameIndex + 2 &&
            tokens[objectNameIndex + 1].Kind == SqlTokenKind.Symbol &&
            tokens[objectNameIndex + 1].Text == "." &&
            tokens[objectNameIndex + 2].Kind is SqlTokenKind.Word or SqlTokenKind.Identifier)
        {
            objectName = $"{objectName}.{tokens[objectNameIndex + 2].Text.Trim().TrimEnd(';', ',').Trim('(', ')')}";
        }

        return new SchemaRefreshTarget(action, objectType, objectName);
    }

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
    }
}

public enum SchemaRefreshAction
{
    Unknown,
    Create,
    Alter,
    Drop,
}

public enum SchemaObjectType
{
    Unknown,
    Table,
    View,
    Procedure,
    Function,
}

public record SchemaRefreshTarget(SchemaRefreshAction Action, SchemaObjectType ObjectType, string? ObjectName);
