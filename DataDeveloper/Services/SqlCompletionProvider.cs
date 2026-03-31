using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
namespace DataDeveloper.Services;

public static class SqlCompletionProvider
{
    private const string IdentifierPattern = @"(?:\[[^\]]+\]|`[^`]+`|""[^""]+""|[A-Za-z_][A-Za-z0-9_]*)";

    private static readonly Regex AliasRegex = new(
        $@"(?ix)
        \b(?:from|join|update|into)\s+
        (?<table>{IdentifierPattern}(?:\.{IdentifierPattern})?)
        \s+(?:as\s+)?
        (?<alias>{IdentifierPattern})",
        RegexOptions.Compiled);
    private static readonly Regex InsertTargetRegex = new(
        $@"(?ix)\binsert\s+into\s+(?<table>{IdentifierPattern}(?:\.{IdentifierPattern})?)",
        RegexOptions.Compiled);
    private static readonly Regex UpdateTargetRegex = new(
        $@"(?ix)\bupdate\s+(?<table>{IdentifierPattern}(?:\.{IdentifierPattern})?)",
        RegexOptions.Compiled);
    private static readonly Regex DeleteTargetRegex = new(
        $@"(?ix)\bdelete(?:\s+\w+)?\s+from\s+(?<table>{IdentifierPattern}(?:\.{IdentifierPattern})?)",
        RegexOptions.Compiled);
    private static readonly Regex SetKeywordRegex = new(
        @"(?ix)\bset\b",
        RegexOptions.Compiled);

    private static readonly Regex CteRegex = new(
        $@"(?ix)
        (?:
            \bwith\s+
          | ,
        )
        (?<name>{IdentifierPattern})
        \s*
        (?:\((?<columns>[^)]*)\)\s*)?
        as\s*\(",
        RegexOptions.Compiled);

    private static readonly ConcurrentDictionary<Guid, SchemaCompletionCache> SchemaCache = new();

    public static bool ShouldTriggerCompletion(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        return text.All(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '.' || ch == ',' || ch == '(');
    }

    public static CompletionRequest? GetAutoCompletionRequest(string editorText, int caretOffset, string? insertedText)
    {
        if (string.IsNullOrEmpty(insertedText))
            return null;

        var context = DetectContext(editorText, caretOffset);

        if (insertedText == ".")
            return new CompletionRequest(context with { Trigger = CompletionTrigger.Columns });

        if (insertedText == ",")
        {
            if (context.IsInsideInsertColumnList)
                return new CompletionRequest(context with { Trigger = CompletionTrigger.Columns });
            if (context.IsInsideUpdateSetList)
                return new CompletionRequest(context with { Trigger = CompletionTrigger.Columns });

            return context.Clause switch
            {
                SqlClause.From or SqlClause.Join or SqlClause.Into or SqlClause.Update
                    => new CompletionRequest(context with { Trigger = CompletionTrigger.Objects }),
                SqlClause.Select or SqlClause.Set
                    => new CompletionRequest(context with { Trigger = CompletionTrigger.Columns }),
                _ => null
            };
        }

        if (insertedText == "(" && context.IsInsideInsertColumnList)
            return new CompletionRequest(context with { Trigger = CompletionTrigger.Columns });

        if (insertedText.All(char.IsWhiteSpace))
        {
            return context.Trigger switch
            {
                CompletionTrigger.Columns => new CompletionRequest(context),
                CompletionTrigger.Objects => new CompletionRequest(context),
                _ => null
            };
        }

        if (!insertedText.All(ch => char.IsLetterOrDigit(ch) || ch == '_'))
            return null;

        return context.Trigger switch
        {
            CompletionTrigger.Columns => new CompletionRequest(context),
            CompletionTrigger.Objects => new CompletionRequest(context),
            _ => null
        };
    }

    public static CompletionRequest GetManualCompletionRequest(string editorText, int caretOffset)
    {
        return new CompletionRequest(DetectContext(editorText, caretOffset));
    }

    public static async Task<IReadOnlyList<ICompletionData>> GetCompletionsAsync(
        IConnectionSettings connectionSettings,
        string editorText,
        int caretOffset,
        CompletionRequest request)
    {
        var currentWord = GetCurrentWord(editorText, caretOffset);
        var cteDefinitions = ExtractCteDefinitions(editorText);
        var completions = new Dictionary<string, SqlCompletionData>(StringComparer.OrdinalIgnoreCase);

        var cache = SchemaCache.GetOrAdd(connectionSettings.Id, _ => new SchemaCompletionCache());
        await EnsureTablesLoadedAsync(connectionSettings, cache);

        if (request.Context.Trigger is CompletionTrigger.Objects or CompletionTrigger.Any)
        {
            foreach (var table in cache.Tables)
            {
                completions.TryAdd(table, new SqlCompletionData(table, "Table", CompletionItemKind.Table, GetObjectPriority(CompletionKind.Table, request.Context.Clause)));
            }

            foreach (var cteName in cteDefinitions.Keys)
            {
                completions.TryAdd(cteName, new SqlCompletionData(cteName, "CTE", CompletionItemKind.Cte, GetObjectPriority(CompletionKind.Cte, request.Context.Clause)));
            }
        }

        if (request.Context.Trigger is CompletionTrigger.Columns or CompletionTrigger.Any)
        {
            var sources = ResolveColumnSources(cache, editorText, request.Context, cteDefinitions);
            foreach (var source in sources)
            {
                var columns = await GetColumnsForSourceAsync(connectionSettings, cache, source, cteDefinitions);
                foreach (var column in columns)
                {
                    completions.TryAdd(column, new SqlCompletionData(column, $"Column from {source.Name}", CompletionItemKind.Column, GetObjectPriority(CompletionKind.Column, request.Context.Clause)));
                }
            }
        }

        return completions.Values
            .Where(item => currentWord.Length == 0 || item.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.Text)
            .Cast<ICompletionData>()
            .ToList();
    }

    public static int GetCompletionStartOffset(string editorText, int caretOffset)
    {
        var currentWord = GetCurrentWord(editorText, caretOffset);
        return Math.Max(0, caretOffset - currentWord.Length);
    }

    private static async Task EnsureTablesLoadedAsync(IConnectionSettings connectionSettings, SchemaCompletionCache cache)
    {
        if (cache.TablesLoaded)
            return;

        var schemaExplorer = connectionSettings.GetSchemaExplorer();
        await schemaExplorer.InitializeSchemaNode();

        var tablesNode = schemaExplorer.RootConnections
            .SelectMany(root => root.Children)
            .FirstOrDefault(node => node.NodeType == NodeType.Tables);

        if (tablesNode is null)
        {
            cache.TablesLoaded = true;
            return;
        }

        cache.TableNodes.Clear();
        foreach (var tableNode in tablesNode.Children.Where(node => node.NodeType == NodeType.Table))
        {
            cache.Tables.Add(tableNode.Name);
            cache.TableNodes[tableNode.Name] = tableNode;
            cache.TableNodes[$"[{tableNode.Name}]"] = tableNode;
            cache.TableNodes[$"`{tableNode.Name}`"] = tableNode;
            cache.TableNodes[$"\"{tableNode.Name}\""] = tableNode;
        }

        cache.TablesLoaded = true;
    }

    private static async Task EnsureColumnsLoadedAsync(IConnectionSettings connectionSettings, SchemaCompletionCache cache, string tableName)
    {
        if (cache.LoadedTables.Contains(tableName))
            return;

        await EnsureTablesLoadedAsync(connectionSettings, cache);

        if (!cache.TableNodes.TryGetValue(tableName, out var tableNode))
            return;

        var schemaExplorer = connectionSettings.GetSchemaExplorer();
        await schemaExplorer.LoadTableColumnsAsync(tableNode);

        cache.ColumnsByTable[tableName] = tableNode.Children
            .Where(node => node.NodeType == NodeType.Column)
            .Select(node => node.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToArray();

        cache.LoadedTables.Add(tableName);
    }

    private static async Task<string[]> GetColumnsForSourceAsync(
        IConnectionSettings connectionSettings,
        SchemaCompletionCache cache,
        CompletionSource source,
        IReadOnlyDictionary<string, string[]> cteDefinitions)
    {
        if (source.Kind == CompletionKind.Cte)
            return cteDefinitions.TryGetValue(source.Name, out var cteColumns) ? cteColumns : Array.Empty<string>();

        await EnsureColumnsLoadedAsync(connectionSettings, cache, source.Name);
        return cache.ColumnsByTable.TryGetValue(source.Name, out var tableColumns) ? tableColumns : Array.Empty<string>();
    }

    private static CompletionSource? ResolveSource(
        SchemaCompletionCache cache,
        string editorText,
        string objectName,
        IReadOnlyDictionary<string, string[]> cteDefinitions)
    {
        if (cache.TableNodes.ContainsKey(objectName))
            return new CompletionSource(cache.TableNodes[objectName].Name, CompletionKind.Table);

        if (cteDefinitions.ContainsKey(objectName))
            return new CompletionSource(objectName, CompletionKind.Cte);

        var aliases = ExtractAliases(editorText, cteDefinitions);
        if (!aliases.TryGetValue(objectName, out var source))
            return null;

        return source;
    }

    private static IReadOnlyList<CompletionSource> ResolveColumnSources(
        SchemaCompletionCache cache,
        string editorText,
        CompletionContext context,
        IReadOnlyDictionary<string, string[]> cteDefinitions)
    {
        if (!string.IsNullOrWhiteSpace(context.ObjectNameBeforeDot))
        {
            var explicitSource = ResolveSource(cache, editorText, context.ObjectNameBeforeDot, cteDefinitions);
            return explicitSource is null ? Array.Empty<CompletionSource>() : [explicitSource];
        }

        var aliases = ExtractAliases(editorText, cteDefinitions);
        if (aliases.Count > 0)
            return aliases.Values.Distinct().ToArray();

        // For INSERT INTO ... ( ... ) and UPDATE ... SET ..., the target table is the
        // most useful fallback source. For SELECT clauses, prefer explicit sources only.
        if (!string.IsNullOrWhiteSpace(context.TargetTableName) &&
            context.Clause is SqlClause.Into or SqlClause.Set or SqlClause.Where or SqlClause.Delete)
            return [new CompletionSource(context.TargetTableName, CompletionKind.Table)];

        if (cteDefinitions.Count > 0)
            return cteDefinitions.Keys.Select(name => new CompletionSource(name, CompletionKind.Cte)).ToArray();

        return cache.TableNodes.Values
            .DistinctBy(node => node.Name)
            .Select(node => new CompletionSource(node.Name, CompletionKind.Table))
            .ToArray();
    }

    private static string GetCurrentWord(string editorText, int caretOffset)
    {
        if (string.IsNullOrEmpty(editorText) || caretOffset <= 0)
            return string.Empty;

        var start = caretOffset;
        while (start > 0)
        {
            var ch = editorText[start - 1];
            if (!char.IsLetterOrDigit(ch) && ch != '_')
                break;

            start--;
        }

        return editorText[start..caretOffset];
    }

    private static string? GetObjectNameBeforeDot(string editorText, int caretOffset)
    {
        if (string.IsNullOrEmpty(editorText) || caretOffset <= 1)
            return null;

        var index = caretOffset - 1;
        while (index >= 0 && IsIdentifierCharacter(editorText[index], allowClosingQuoteOnly: true))
            index--;

        if (index < 0 || editorText[index] != '.')
            return null;

        var end = index;
        index--;
        while (index >= 0 && IsIdentifierCharacter(editorText[index], allowClosingQuoteOnly: false))
            index--;

        var objectName = editorText[(index + 1)..end];
        return string.IsNullOrWhiteSpace(objectName) ? null : NormalizeIdentifier(objectName);
    }

    private static Dictionary<string, CompletionSource> ExtractAliases(
        string editorText,
        IReadOnlyDictionary<string, string[]> cteDefinitions)
    {
        var aliases = new Dictionary<string, CompletionSource>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in AliasRegex.Matches(editorText))
        {
            var alias = NormalizeIdentifier(match.Groups["alias"].Value);
            var sourceName = NormalizeIdentifier(match.Groups["table"].Value);
            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(sourceName))
                continue;

            sourceName = sourceName.Contains('.') ? sourceName.Split('.').Last() : sourceName;
            var kind = cteDefinitions.ContainsKey(sourceName) ? CompletionKind.Cte : CompletionKind.Table;
            aliases[alias] = new CompletionSource(sourceName, kind);
        }

        return aliases;
    }

    private static Dictionary<string, string[]> ExtractCteDefinitions(string editorText)
    {
        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in CteRegex.Matches(editorText))
        {
            var cteName = NormalizeIdentifier(match.Groups["name"].Value);
            if (string.IsNullOrWhiteSpace(cteName))
                continue;

            var explicitColumns = ParseExplicitCteColumns(match.Groups["columns"].Value);
            if (explicitColumns.Length > 0)
            {
                result[cteName] = explicitColumns;
                continue;
            }

            var bodyStart = match.Index + match.Length;
            var cteBody = ExtractParenthesizedContent(editorText, bodyStart);
            result[cteName] = InferColumnsFromQuery(cteBody);
        }

        return result;
    }

    private static CompletionContext DetectContext(string editorText, int caretOffset)
    {
        if (string.IsNullOrWhiteSpace(editorText) || caretOffset <= 0)
            return new CompletionContext(SqlClause.None, CompletionTrigger.Any, null, null, false, false);

        var textBeforeCaret = editorText[..Math.Min(caretOffset, editorText.Length)];
        var targetTableName = DetectTargetTableName(textBeforeCaret);
        var isInsideInsertColumnList = IsInsideInsertColumnList(textBeforeCaret);
        var isInsideUpdateSetList = IsInsideUpdateSetList(textBeforeCaret);
        var objectNameBeforeDot = GetObjectNameBeforeDot(editorText, caretOffset);
        if (!string.IsNullOrWhiteSpace(objectNameBeforeDot))
            return new CompletionContext(SqlClause.None, CompletionTrigger.Columns, objectNameBeforeDot, targetTableName, isInsideInsertColumnList, isInsideUpdateSetList);

        var matches = Regex.Matches(textBeforeCaret, @"(?ix)\b(select|from|join|where|group\s+by|order\s+by|update|into|set|delete)\b");
        if (matches.Count == 0)
            return new CompletionContext(SqlClause.None, CompletionTrigger.Any, null, targetTableName, isInsideInsertColumnList, isInsideUpdateSetList);

        var token = matches[^1].Value.ToUpperInvariant().Replace(" ", "");
        var clause = token switch
        {
            "SELECT" => SqlClause.Select,
            "FROM" => SqlClause.From,
            "JOIN" => SqlClause.Join,
            "WHERE" => SqlClause.Where,
            "GROUPBY" => SqlClause.GroupBy,
            "ORDERBY" => SqlClause.OrderBy,
            "UPDATE" => SqlClause.Update,
            "INTO" => SqlClause.Into,
            "SET" => SqlClause.Set,
            "DELETE" => SqlClause.Delete,
            _ => SqlClause.None
        };

        var trigger = clause switch
        {
            _ when isInsideInsertColumnList => CompletionTrigger.Columns,
            _ when isInsideUpdateSetList => CompletionTrigger.Columns,
            SqlClause.From or SqlClause.Join or SqlClause.Into or SqlClause.Update => CompletionTrigger.Objects,
            SqlClause.Select or SqlClause.Where or SqlClause.GroupBy or SqlClause.OrderBy or SqlClause.Set => CompletionTrigger.Columns,
            _ => CompletionTrigger.Any
        };

        return new CompletionContext(clause, trigger, null, targetTableName, isInsideInsertColumnList, isInsideUpdateSetList);
    }

    private static double GetObjectPriority(CompletionKind kind, SqlClause clause)
    {
        return (kind, clause) switch
        {
            (CompletionKind.Column, SqlClause.Select) => -30,
            (CompletionKind.Column, SqlClause.Where) => -25,
            (CompletionKind.Column, SqlClause.GroupBy) => -25,
            (CompletionKind.Column, SqlClause.OrderBy) => -25,
            (CompletionKind.Column, SqlClause.Set) => -35,
            (CompletionKind.Column, SqlClause.Into) => -35,
            (CompletionKind.Table, SqlClause.From) => -30,
            (CompletionKind.Table, SqlClause.Join) => -30,
            (CompletionKind.Table, SqlClause.Delete) => -30,
            (CompletionKind.Table, SqlClause.Into) => -30,
            (CompletionKind.Cte, SqlClause.From) => -35,
            (CompletionKind.Cte, SqlClause.Join) => -35,
            _ => 0
        };
    }

    private static string? DetectTargetTableName(string textBeforeCaret)
    {
        Match? latestMatch = null;
        foreach (var regex in new[] { UpdateTargetRegex, InsertTargetRegex, DeleteTargetRegex })
        {
            var match = GetLastMatch(regex, textBeforeCaret);
            if (match.Success && (latestMatch is null || match.Index > latestMatch.Index))
                latestMatch = match;
        }

        if (latestMatch is null)
            return null;

        var tableName = NormalizeIdentifier(latestMatch.Groups["table"].Value);
        if (tableName.Contains('.'))
            tableName = tableName.Split('.').Last();

        return string.IsNullOrWhiteSpace(tableName) ? null : tableName;
    }

    private static bool IsInsideInsertColumnList(string textBeforeCaret)
    {
        var insertMatch = GetLastMatch(InsertTargetRegex, textBeforeCaret);
        if (!insertMatch.Success)
            return false;

        var searchStartIndex = insertMatch.Index + insertMatch.Length;
        var valuesIndex = textBeforeCaret.IndexOf("values", searchStartIndex, StringComparison.OrdinalIgnoreCase);
        var selectIndex = textBeforeCaret.IndexOf("select", searchStartIndex, StringComparison.OrdinalIgnoreCase);
        var stopIndex = new[] { valuesIndex, selectIndex }
            .Where(index => index >= 0)
            .DefaultIfEmpty(textBeforeCaret.Length)
            .Min();

        if (stopIndex < searchStartIndex)
            return false;

        var slice = textBeforeCaret[..stopIndex];
        var openParenIndex = slice.IndexOf('(', searchStartIndex);
        if (openParenIndex < 0)
            return false;

        var depth = 0;
        for (var i = openParenIndex; i < slice.Length; i++)
        {
            switch (slice[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth == 0)
                        return false;
                    break;
            }
        }

        return depth > 0;
    }

    private static bool IsInsideUpdateSetList(string textBeforeCaret)
    {
        var updateMatch = GetLastMatch(UpdateTargetRegex, textBeforeCaret);
        if (!updateMatch.Success)
            return false;

        var setMatch = GetLastMatch(SetKeywordRegex, textBeforeCaret);
        if (!setMatch.Success || setMatch.Index < updateMatch.Index)
            return false;

        var stopIndex = new[]
        {
            textBeforeCaret.IndexOf("where", setMatch.Index, StringComparison.OrdinalIgnoreCase),
            textBeforeCaret.IndexOf("from", setMatch.Index, StringComparison.OrdinalIgnoreCase)
        }
        .Where(index => index >= 0)
        .DefaultIfEmpty(textBeforeCaret.Length)
        .Min();

        return setMatch.Index < stopIndex;
    }

    private static Match GetLastMatch(Regex regex, string text)
    {
        Match? lastMatch = null;
        foreach (Match match in regex.Matches(text))
            lastMatch = match;

        return lastMatch ?? Match.Empty;
    }

    private static string NormalizeIdentifier(string value)
    {
        return value.Trim().Trim('[', ']', '`', '"');
    }

    private static bool IsIdentifierCharacter(char ch, bool allowClosingQuoteOnly)
    {
        return char.IsLetterOrDigit(ch) ||
               ch == '_' ||
               ch == '[' ||
               ch == ']' ||
               ch == '`' ||
               ch == '"' ||
               (!allowClosingQuoteOnly && ch == '.');
    }

    private static string[] ParseExplicitCteColumns(string columnsValue)
    {
        if (string.IsNullOrWhiteSpace(columnsValue))
            return Array.Empty<string>();

        return columnsValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeIdentifier)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ExtractParenthesizedContent(string text, int startIndex)
    {
        var depth = 1;
        for (var i = startIndex; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth == 0)
                        return text[startIndex..i];
                    break;
            }
        }

        return text[startIndex..];
    }

    private static string[] InferColumnsFromQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<string>();

        var selectIndex = query.IndexOf("select", StringComparison.OrdinalIgnoreCase);
        if (selectIndex < 0)
            return Array.Empty<string>();

        var fromIndex = FindTopLevelFrom(query, selectIndex + 6);
        var projection = fromIndex > selectIndex ? query[(selectIndex + 6)..fromIndex] : query[(selectIndex + 6)..];

        return SplitTopLevel(projection)
            .Select(InferColumnName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int FindTopLevelFrom(string query, int startIndex)
    {
        var depth = 0;
        for (var i = startIndex; i < query.Length - 3; i++)
        {
            switch (query[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth = Math.Max(0, depth - 1);
                    break;
            }

            if (depth == 0 &&
                i + 4 <= query.Length &&
                string.Equals(query.Substring(i, 4), "from", StringComparison.OrdinalIgnoreCase) &&
                IsWordBoundary(query, i - 1) &&
                IsWordBoundary(query, i + 4))
            {
                return i;
            }
        }

        return -1;
    }

    private static IEnumerable<string> SplitTopLevel(string value)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < value.Length; i++)
        {
            switch (value[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth = Math.Max(0, depth - 1);
                    break;
                case ',' when depth == 0:
                    yield return value[start..i].Trim();
                    start = i + 1;
                    break;
            }
        }

        if (start < value.Length)
            yield return value[start..].Trim();
    }

    private static string InferColumnName(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return string.Empty;

        var asMatch = Regex.Match(expression, $@"(?ix)\bas\s+(?<alias>{IdentifierPattern})\s*$");
        if (asMatch.Success)
            return NormalizeIdentifier(asMatch.Groups["alias"].Value);

        var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 1)
            return NormalizeIdentifier(parts[^1]);

        var lastDot = expression.LastIndexOf('.');
        var candidate = lastDot >= 0 ? expression[(lastDot + 1)..] : expression;
        return NormalizeIdentifier(candidate.Trim());
    }

    private static bool IsWordBoundary(string text, int index)
    {
        return index < 0 || index >= text.Length || (!char.IsLetterOrDigit(text[index]) && text[index] != '_');
    }

    private sealed class SchemaCompletionCache
    {
        public bool TablesLoaded { get; set; }
        public HashSet<string> Tables { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, SchemaNode> TableNodes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string[]> ColumnsByTable { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> LoadedTables { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public enum SqlClause
    {
        None,
        Select,
        From,
        Join,
        Where,
        GroupBy,
        OrderBy,
        Update,
        Into,
        Set,
        Delete
    }

    private enum CompletionKind
    {
        Column,
        Table,
        Cte
    }

    private sealed record CompletionSource(string Name, CompletionKind Kind);

    internal sealed record CompletionContext(
        SqlClause Clause,
        CompletionTrigger Trigger,
        string? ObjectNameBeforeDot,
        string? TargetTableName,
        bool IsInsideInsertColumnList,
        bool IsInsideUpdateSetList);
}

public sealed class CompletionRequest
{
    internal CompletionRequest(SqlCompletionProvider.CompletionContext context)
    {
        Context = context;
    }

    internal SqlCompletionProvider.CompletionContext Context { get; }
}

public enum CompletionTrigger
{
    Any,
    Columns,
    Objects
}

public enum CompletionItemKind
{
    Table,
    Column,
    Cte
}

public sealed record SqlCompletionData(string Text, string Description, CompletionItemKind Kind, double Priority = 0) : ICompletionData
{
    public IImage? Image => null;
    public object Content => BuildContent();
    object ICompletionData.Description => Description;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }

    private object BuildContent()
    {
        var fontAwesome = TryGetFont("FontAwesomeSolid");
        var monospace = TryGetFont("MonospaceFont");

        var icon = new Avalonia.Controls.TextBlock
        {
            Text = GetIconGlyph(),
            Foreground = GetForeground(),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 0, 8, 0)
        };
        if (fontAwesome is not null)
            icon.FontFamily = fontAwesome;

        var text = new Avalonia.Controls.TextBlock
        {
            Text = Text,
            Foreground = GetForeground(),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        if (monospace is not null)
            text.FontFamily = monospace;

        var kind = new Avalonia.Controls.TextBlock
        {
            Text = Description,
            Foreground = Avalonia.Media.Brushes.Gray,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(10, 0, 0, 0),
            FontSize = 11
        };

        return new Avalonia.Controls.StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Children = { icon, text, kind }
        };
    }

    private Avalonia.Media.FontFamily? TryGetFont(string key)
    {
        return Avalonia.Application.Current?.Resources[key] as Avalonia.Media.FontFamily;
    }

    private Avalonia.Media.IBrush GetForeground()
    {
        return Kind switch
        {
            CompletionItemKind.Table => Avalonia.Media.Brushes.DeepSkyBlue,
            CompletionItemKind.Column => Avalonia.Media.Brushes.LightGreen,
            CompletionItemKind.Cte => Avalonia.Media.Brushes.Goldenrod,
            _ => Avalonia.Media.Brushes.White
        };
    }

    private string GetIconGlyph()
    {
        return Kind switch
        {
            CompletionItemKind.Table => "\uf00b",
            CompletionItemKind.Column => "\uf0ca",
            CompletionItemKind.Cte => "\uf1c0",
            _ => "\uf15b"
        };
    }
}
