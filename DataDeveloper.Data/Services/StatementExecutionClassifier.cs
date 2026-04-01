namespace DataDeveloper.Data.Services;

public static class StatementExecutionClassifier
{
    public static bool RequiresMaterialization(string statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
            return false;

        var trimmed = TrimLeadingTrivia(statement);
        return trimmed.StartsWith("exec ", System.StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("execute ", System.StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("call ", System.StringComparison.OrdinalIgnoreCase) ||
               (trimmed.StartsWith("begin", System.StringComparison.OrdinalIgnoreCase) && ContainsStandaloneExecOrCall(trimmed));
    }

    public static bool RequiresSchemaRefresh(string statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
            return false;

        var trimmed = TrimLeadingTrivia(statement);
        return trimmed.StartsWith("create ", System.StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("alter ", System.StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("drop ", System.StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("truncate ", System.StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("rename ", System.StringComparison.OrdinalIgnoreCase);
    }

    public static SchemaRefreshTarget? ParseSchemaRefreshTarget(string statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
            return null;

        var trimmed = TrimLeadingTrivia(statement).TrimStart();
        var tokens = trimmed
            .Split((char[]?)null, 6, System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

        if (tokens.Length < 2)
            return null;

        var action = tokens[0];
        var objectKeyword = tokens[1];

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

    private static string TrimLeadingTrivia(string statement)
    {
        var index = 0;
        while (index < statement.Length)
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

            break;
        }

        return statement[index..];
    }

    private static bool ContainsStandaloneExecOrCall(string statement)
    {
        return ContainsStandaloneKeyword(statement, "exec") ||
               ContainsStandaloneKeyword(statement, "execute") ||
               ContainsStandaloneKeyword(statement, "call");
    }

    private static bool ContainsStandaloneKeyword(string statement, string keyword)
    {
        var index = 0;
        while (index >= 0 && index < statement.Length)
        {
            index = statement.IndexOf(keyword, index, System.StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return false;

            var beforeIsIdentifier = index > 0 && IsIdentifierChar(statement[index - 1]);
            var afterIndex = index + keyword.Length;
            var afterIsIdentifier = afterIndex < statement.Length && IsIdentifierChar(statement[afterIndex]);
            if (!beforeIsIdentifier && !afterIsIdentifier)
                return true;

            index = afterIndex;
        }

        return false;
    }

    private static bool IsIdentifierChar(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_' || value == '@';
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

    private static SchemaRefreshTarget? BuildTarget(string[] tokens, int objectNameIndex, SchemaRefreshAction action, SchemaObjectType objectType)
    {
        if (tokens.Length <= objectNameIndex)
            return new SchemaRefreshTarget(action, objectType, null);

        var objectName = tokens[objectNameIndex]
            .Trim()
            .TrimEnd(';', ',')
            .Trim('(', ')');

        return new SchemaRefreshTarget(action, objectType, objectName);
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
