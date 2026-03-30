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
}
