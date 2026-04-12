using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;

namespace DataDeveloper.Data.Services;

public class ProviderSqlAnalyzer : IProviderSqlAnalyzer
{
    private readonly DatabaseType _databaseType;

    public ProviderSqlAnalyzer(DatabaseType databaseType)
    {
        _databaseType = databaseType;
    }

    public static IProviderSqlAnalyzer Create(DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.SqlServer => new SqlServerSqlAnalyzer(),
            DatabaseType.Oracle => new OracleSqlAnalyzer(),
            DatabaseType.PostgresSql => new PostgresSqlAnalyzer(),
            DatabaseType.MySql => new MySqlSqlAnalyzer(),
            DatabaseType.SqLite => new SqLiteSqlAnalyzer(),
            _ => throw new NotSupportedException($"Database type {databaseType} is not implemented")
        };
    }

    public IReadOnlyList<string> SplitStatements(string sqlText)
    {
        return StatementSplitter.SplitStatements(sqlText, _databaseType);
    }

    public virtual bool RequiresMaterialization(string statement)
    {
        var tokens = Tokenize(statement);
        if (tokens.Count == 0)
            return false;

        return IsRoutineInvocation(tokens, 0);
    }

    public virtual bool RequiresSchemaRefresh(string statement)
    {
        var tokens = Tokenize(statement);
        if (tokens.Count == 0)
            return false;

        return tokens[0].UpperText is "CREATE" or "ALTER" or "DROP" or "TRUNCATE" or "RENAME";
    }

    public virtual bool IsDmlStatement(string statement)
    {
        var tokens = Tokenize(statement);
        if (tokens.Count == 0)
            return false;

        return tokens[0].UpperText is "INSERT" or "UPDATE" or "DELETE" or "MERGE";
    }

    public virtual bool IsTransactionControlStatement(string statement)
    {
        return IsBeginTransactionStatement(statement) ||
               StartsWithKeyword(statement, "commit") ||
               StartsWithKeyword(statement, "rollback");
    }

    public virtual bool IsBeginTransactionStatement(string statement)
    {
        return StartsWithKeywords(statement, "begin", "transaction") ||
               StartsWithKeywords(statement, "begin", "tran") ||
               StartsWithKeywords(statement, "begin", "work");
    }

    public virtual SchemaRefreshTarget? ParseSchemaRefreshTarget(string statement)
    {
        return ParseStandardSchemaRefreshTarget(statement);
    }

    protected static bool StartsWithKeyword(string statement, string keyword)
    {
        var keywords = ReadLeadingKeywords(statement, 1);
        return keywords.Count >= 1 &&
               string.Equals(keywords[0], keyword, StringComparison.OrdinalIgnoreCase);
    }

    protected static bool StartsWithKeywords(string statement, params string[] expectedKeywords)
    {
        var keywords = ReadLeadingKeywords(statement, expectedKeywords.Length);
        if (keywords.Count < expectedKeywords.Length)
            return false;

        for (var index = 0; index < expectedKeywords.Length; index++)
        {
            if (!string.Equals(keywords[index], expectedKeywords[index], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    protected static SchemaRefreshTarget? ParseStandardSchemaRefreshTarget(string statement)
    {
        var tokens = Tokenize(statement);
        if (tokens.Count < 2)
            return null;

        var actionText = tokens[0].Text;
        var index = 1;

        if (IsKeyword(actionText, "rename"))
            return new SchemaRefreshTarget(SchemaRefreshAction.Unknown, SchemaObjectType.Unknown, null);

        if (IsKeyword(actionText, "truncate") && IsToken(tokens, index, "table"))
            return BuildTarget(tokens, index + 1, SchemaRefreshAction.Alter, SchemaObjectType.Table);

        if (!TryMapAction(actionText, out var action))
            return null;

        if (action == SchemaRefreshAction.Create)
        {
            if (IsToken(tokens, index, "or") &&
                (IsToken(tokens, index + 1, "alter") || IsToken(tokens, index + 1, "replace")))
            {
                index += 2;
            }

            while (IsToken(tokens, index, "temporary") ||
                   IsToken(tokens, index, "temp") ||
                   IsToken(tokens, index, "global") ||
                   IsToken(tokens, index, "local") ||
                   IsToken(tokens, index, "editionable") ||
                   IsToken(tokens, index, "noneditionable"))
            {
                index++;
            }
        }

        if (!TryReadObjectType(tokens, index, out var objectType, out var nextIndex))
            return null;

        index = nextIndex;
        if (action == SchemaRefreshAction.Drop &&
            IsToken(tokens, index, "if") &&
            IsToken(tokens, index + 1, "exists"))
        {
            index += 2;
        }

        return BuildTarget(tokens, index, action, objectType);
    }

    protected static List<string> ReadLeadingKeywords(string statement, int maxKeywords)
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
            while (index < statement.Length && IsIdentifierChar(statement[index]))
                index++;

            keywords.Add(statement[start..index]);
        }

        return keywords;
    }

    private static bool IsIdentifierChar(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_' || value == '@' || value == '$';
    }

    protected static List<SqlAnalysisToken> Tokenize(string statement)
    {
        var tokens = new List<SqlAnalysisToken>();
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

                tokens.Add(new SqlAnalysisToken(statement[start..index], SqlAnalysisTokenKind.Identifier));
                continue;
            }

            if (char.IsLetter(statement[index]) || statement[index] == '_')
            {
                var start = index++;
                while (index < statement.Length && IsIdentifierChar(statement[index]))
                    index++;

                tokens.Add(new SqlAnalysisToken(statement[start..index], SqlAnalysisTokenKind.Word));
                continue;
            }

            if (char.IsDigit(statement[index]))
            {
                var start = index++;
                while (index < statement.Length && char.IsDigit(statement[index]))
                    index++;

                tokens.Add(new SqlAnalysisToken(statement[start..index], SqlAnalysisTokenKind.Other));
                continue;
            }

            tokens.Add(new SqlAnalysisToken(statement[index].ToString(), SqlAnalysisTokenKind.Symbol));
            index++;
        }

        return tokens;
    }

    private static bool TryReadObjectType(
        IReadOnlyList<SqlAnalysisToken> tokens,
        int index,
        out SchemaObjectType objectType,
        out int nextIndex)
    {
        objectType = SchemaObjectType.Unknown;
        nextIndex = index;

        if (IsToken(tokens, index, "materialized") && IsToken(tokens, index + 1, "view"))
        {
            objectType = SchemaObjectType.View;
            nextIndex = index + 2;
            return true;
        }

        if (!TryMapObjectType(tokens.ElementAtOrDefault(index).Text, out objectType))
            return false;

        nextIndex = index + 1;
        return true;
    }

    private static SchemaRefreshTarget? BuildTarget(
        IReadOnlyList<SqlAnalysisToken> tokens,
        int objectNameIndex,
        SchemaRefreshAction action,
        SchemaObjectType objectType)
    {
        if (tokens.Count <= objectNameIndex)
            return new SchemaRefreshTarget(action, objectType, null);

        var objectName = NormalizeObjectNameToken(tokens[objectNameIndex].Text);

        if (tokens.Count > objectNameIndex + 2 &&
            tokens[objectNameIndex + 1].Text == "." &&
            tokens[objectNameIndex + 2].Kind is SqlAnalysisTokenKind.Word or SqlAnalysisTokenKind.Identifier)
        {
            objectName = $"{objectName}.{NormalizeObjectNameToken(tokens[objectNameIndex + 2].Text)}";
        }

        return new SchemaRefreshTarget(action, objectType, objectName);
    }

    private static string NormalizeObjectNameToken(string value)
    {
        return value
            .Trim()
            .TrimEnd(';', ',')
            .Trim('(', ')')
            .Trim('[', ']', '"', '`');
    }

    private static bool IsToken(IReadOnlyList<SqlAnalysisToken> tokens, int index, string value)
    {
        return index >= 0 &&
               index < tokens.Count &&
               IsKeyword(tokens[index].Text, value);
    }

    private static bool IsKeyword(string? value, string keyword)
    {
        return string.Equals(value, keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryMapAction(string value, out SchemaRefreshAction action)
    {
        if (value.Equals("create", StringComparison.OrdinalIgnoreCase))
        {
            action = SchemaRefreshAction.Create;
            return true;
        }

        if (value.Equals("alter", StringComparison.OrdinalIgnoreCase))
        {
            action = SchemaRefreshAction.Alter;
            return true;
        }

        if (value.Equals("drop", StringComparison.OrdinalIgnoreCase))
        {
            action = SchemaRefreshAction.Drop;
            return true;
        }

        action = SchemaRefreshAction.Unknown;
        return false;
    }

    private static bool TryMapObjectType(string? value, out SchemaObjectType objectType)
    {
        if (value?.Equals("table", StringComparison.OrdinalIgnoreCase) == true)
        {
            objectType = SchemaObjectType.Table;
            return true;
        }

        if (value?.Equals("view", StringComparison.OrdinalIgnoreCase) == true)
        {
            objectType = SchemaObjectType.View;
            return true;
        }

        if (value?.Equals("procedure", StringComparison.OrdinalIgnoreCase) == true ||
            value?.Equals("proc", StringComparison.OrdinalIgnoreCase) == true)
        {
            objectType = SchemaObjectType.Procedure;
            return true;
        }

        if (value?.Equals("function", StringComparison.OrdinalIgnoreCase) == true)
        {
            objectType = SchemaObjectType.Function;
            return true;
        }

        objectType = SchemaObjectType.Unknown;
        return false;
    }

    protected readonly record struct SqlAnalysisToken(string Text, SqlAnalysisTokenKind Kind)
    {
        public string UpperText => Text.ToUpperInvariant();
    }

    protected enum SqlAnalysisTokenKind
    {
        Word,
        Identifier,
        Symbol,
        Other
    }

    protected static bool ContainsRoutineInvocation(IReadOnlyList<SqlAnalysisToken> tokens)
    {
        for (var index = 0; index < tokens.Count; index++)
        {
            if (IsRoutineInvocation(tokens, index))
                return true;
        }

        return false;
    }

    protected static bool IsRoutineInvocation(IReadOnlyList<SqlAnalysisToken> tokens, int index)
    {
        return IsToken(tokens, index, "exec") ||
               IsToken(tokens, index, "execute") ||
               IsToken(tokens, index, "call");
    }
}
