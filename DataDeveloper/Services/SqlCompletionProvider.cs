using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Input;
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

    private static readonly ConcurrentDictionary<Guid, SchemaCompletionCache> SchemaCache = new();

    public static bool ShouldTriggerCompletion(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        return text.All(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) || ch == '_' || ch == '.' || ch == ',' || ch == '(');
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
                    completions.TryAdd(
                        column.Name,
                        new SqlCompletionData(
                            column.Name,
                            FormatColumnCompletionDescription(source.Name, column),
                            CompletionItemKind.Column,
                            GetObjectPriority(CompletionKind.Column, request.Context.Clause)));
                }
            }
        }

        if (ShouldIncludeFunctions(request.Context))
        {
            foreach (var function in SqlFunctionCatalog.GetFunctions(connectionSettings.DatabaseType))
            {
                completions.TryAdd(
                    function.Name,
                    new SqlCompletionData(
                        function.Name,
                        function.Description,
                        CompletionItemKind.Function,
                        GetFunctionPriority(request.Context.Clause, function.Category),
                        $"returns {function.ReturnType}"));
            }
        }

        return completions.Values
            .Where(item => currentWord.Length == 0 || item.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.Text)
            .Cast<ICompletionData>()
            .ToList();
    }

    private static bool ShouldIncludeFunctions(CompletionContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.ObjectNameBeforeDot) ||
            context.IsInsideInsertColumnList)
            return false;

        return context.Trigger is CompletionTrigger.Columns or CompletionTrigger.Any &&
               context.Clause is (SqlClause.None or
                   SqlClause.Select or
                   SqlClause.Where or
                   SqlClause.GroupBy or
                   SqlClause.OrderBy or
                   SqlClause.Set or
                   SqlClause.Delete);
    }

    private static double GetFunctionPriority(SqlClause clause, SqlFunctionCategory category)
    {
        var basePriority = clause switch
        {
            SqlClause.Select or SqlClause.Where or SqlClause.Set => 30,
            SqlClause.GroupBy or SqlClause.OrderBy => 35,
            _ => 40
        };

        return category == SqlFunctionCategory.Aggregate &&
               clause is (SqlClause.Select or SqlClause.GroupBy or SqlClause.OrderBy)
            ? basePriority - 2
            : basePriority;
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
            .Select(CreateColumnCompletionInfo)
            .DistinctBy(column => column.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(column => column.Name)
            .ToArray();

        cache.LoadedTables.Add(tableName);
    }

    private static async Task<IReadOnlyList<ColumnCompletionInfo>> GetColumnsForSourceAsync(
        IConnectionSettings connectionSettings,
        SchemaCompletionCache cache,
        CompletionSource source,
        IReadOnlyDictionary<string, string[]> cteDefinitions)
    {
        if (source.Kind == CompletionKind.Cte)
        {
            return cteDefinitions.TryGetValue(source.Name, out var cteColumns)
                ? cteColumns.Select(column => new ColumnCompletionInfo(column, string.Empty, 0, 0, 0)).ToArray()
                : Array.Empty<ColumnCompletionInfo>();
        }

        await EnsureColumnsLoadedAsync(connectionSettings, cache, source.Name);
        return cache.ColumnsByTable.TryGetValue(source.Name, out var tableColumns) ? tableColumns : Array.Empty<ColumnCompletionInfo>();
    }

    private static ColumnCompletionInfo CreateColumnCompletionInfo(SchemaNode node)
    {
        return node.Tag is ColumnModel column
            ? new ColumnCompletionInfo(column.Name, column.DataType, column.Length, column.Precision, column.Scale)
            : new ColumnCompletionInfo(node.Name, string.Empty, 0, 0, 0);
    }

    private static string FormatColumnCompletionDescription(string sourceName, ColumnCompletionInfo column)
    {
        var dataType = FormatColumnDataType(column);
        return string.IsNullOrWhiteSpace(dataType)
            ? $"from {sourceName}"
            : $"from {sourceName} {dataType}";
    }

    private static string FormatColumnDataType(ColumnCompletionInfo column)
    {
        if (string.IsNullOrWhiteSpace(column.DataType))
            return string.Empty;

        var dataType = column.DataType;
        return dataType.ToLowerInvariant() switch
        {
            "varchar" or "nvarchar" or "char" or "nchar" or "binary" or "varbinary"
                => column.Length != 0 ? $"{dataType} ({(column.Length == -1 ? "max" : column.Length)})" : dataType,
            "decimal" or "numeric" or "number"
                => column.Precision != 0 ? $"{dataType}({column.Precision}{(column.Scale != 0 ? $", {column.Scale}" : string.Empty)})" : dataType,
            _ => dataType
        };
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

        var referencedSources = ExtractReferencedSources(editorText, cteDefinitions);
        if (referencedSources.Count > 0)
            return referencedSources;

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

    private static IReadOnlyList<CompletionSource> ExtractReferencedSources(
        string editorText,
        IReadOnlyDictionary<string, string[]> cteDefinitions)
    {
        var sources = new List<CompletionSource>();
        var tokens = TokenizeSql(editorText);

        for (var i = 0; i < tokens.Count; i++)
        {
            if (!IsKeyword(tokens, i, "from") &&
                !IsKeyword(tokens, i, "join") &&
                !IsKeyword(tokens, i, "update") &&
                !IsKeyword(tokens, i, "into"))
            {
                continue;
            }

            var cursor = i + 1;
            if (!TryReadQualifiedName(tokens, ref cursor, out var sourceName))
                continue;

            sourceName = sourceName.Contains('.') ? sourceName.Split('.').Last() : sourceName;
            var kind = cteDefinitions.ContainsKey(sourceName) ? CompletionKind.Cte : CompletionKind.Table;
            sources.Add(new CompletionSource(sourceName, kind));
        }

        return sources
            .Distinct()
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
        var tokens = TokenizeSql(editorText);

        for (var i = 0; i < tokens.Count; i++)
        {
            if (!IsKeyword(tokens, i, "from") &&
                !IsKeyword(tokens, i, "join") &&
                !IsKeyword(tokens, i, "update") &&
                !IsKeyword(tokens, i, "into"))
            {
                continue;
            }

            var cursor = i + 1;
            if (!TryReadQualifiedName(tokens, ref cursor, out var sourceName))
                continue;

            var aliasCursor = cursor;
            if (IsKeyword(tokens, aliasCursor, "as"))
                aliasCursor++;

            if (!TryReadIdentifier(tokens, ref aliasCursor, out var alias) || IsReservedAliasKeyword(alias))
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
        var tokens = TokenizeSql(editorText);
        var index = 0;

        while (index < tokens.Count && tokens[index].Kind == SqlScanTokenKind.Symbol && tokens[index].Text == ";")
            index++;

        if (!IsKeyword(tokens, index, "with"))
            return result;

        index++;
        while (index < tokens.Count)
        {
            if (!TryReadIdentifier(tokens, ref index, out var cteName))
                break;

            string[] explicitColumns = Array.Empty<string>();
            if (index < tokens.Count && tokens[index].Kind == SqlScanTokenKind.Symbol && tokens[index].Text == "(")
            {
                if (!TryReadParenthesizedTokenRange(tokens, ref index, out var columnsStart, out var columnsEnd))
                    break;

                explicitColumns = ParseExplicitCteColumns(editorText[columnsStart..columnsEnd]);
            }

            if (!IsKeyword(tokens, index, "as"))
                break;

            index++;
            if (!TryReadParenthesizedTokenRange(tokens, ref index, out var bodyStart, out var bodyEnd))
                break;

            if (explicitColumns.Length > 0)
            {
                result[cteName] = explicitColumns;
            }
            else
            {
                result[cteName] = InferColumnsFromQuery(editorText[bodyStart..bodyEnd]);
            }

            if (index < tokens.Count && tokens[index].Kind == SqlScanTokenKind.Symbol && tokens[index].Text == ",")
            {
                index++;
                continue;
            }

            break;
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

        var clause = DetectLastClause(textBeforeCaret);
        if (clause == SqlClause.None)
            return new CompletionContext(SqlClause.None, CompletionTrigger.Any, null, targetTableName, isInsideInsertColumnList, isInsideUpdateSetList);

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

    private static SqlClause DetectLastClause(string textBeforeCaret)
    {
        var tokens = TokenizeSql(textBeforeCaret);
        var clause = SqlClause.None;
        var parenDepth = 0;

        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == SqlScanTokenKind.Symbol)
            {
                if (tokens[i].Text == "(")
                {
                    parenDepth++;
                    continue;
                }

                if (tokens[i].Text == ")")
                {
                    if (parenDepth > 0)
                        parenDepth--;
                    continue;
                }
            }

            if (parenDepth > 0)
                continue;

            if (IsKeyword(tokens, i, "group") && IsKeyword(tokens, i + 1, "by"))
            {
                clause = SqlClause.GroupBy;
                i++;
                continue;
            }

            if (IsKeyword(tokens, i, "order") && IsKeyword(tokens, i + 1, "by"))
            {
                clause = SqlClause.OrderBy;
                i++;
                continue;
            }

            if (IsKeyword(tokens, i, "select"))
            {
                clause = SqlClause.Select;
                continue;
            }

            if (IsKeyword(tokens, i, "from"))
            {
                clause = SqlClause.From;
                continue;
            }

            if (IsKeyword(tokens, i, "join") ||
                (IsKeyword(tokens, i, "inner") && IsKeyword(tokens, i + 1, "join")) ||
                (IsKeyword(tokens, i, "left") && IsKeyword(tokens, i + 1, "join")) ||
                (IsKeyword(tokens, i, "right") && IsKeyword(tokens, i + 1, "join")) ||
                (IsKeyword(tokens, i, "full") && IsKeyword(tokens, i + 1, "join")) ||
                (IsKeyword(tokens, i, "cross") && IsKeyword(tokens, i + 1, "join")))
            {
                clause = SqlClause.Join;
                continue;
            }

            if (IsKeyword(tokens, i, "where"))
            {
                clause = SqlClause.Where;
                continue;
            }

            if (IsKeyword(tokens, i, "update"))
            {
                clause = SqlClause.Update;
                continue;
            }

            if (IsKeyword(tokens, i, "into"))
            {
                clause = SqlClause.Into;
                continue;
            }

            if (IsKeyword(tokens, i, "set"))
            {
                clause = SqlClause.Set;
                continue;
            }

            if (IsKeyword(tokens, i, "delete"))
            {
                clause = SqlClause.Delete;
            }
        }

        return clause;
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
        var tokens = TokenizeSql(textBeforeCaret);
        string? targetTable = null;

        for (var i = 0; i < tokens.Count; i++)
        {
            var cursor = i;
            if (IsKeyword(tokens, cursor, "insert") && IsKeyword(tokens, cursor + 1, "into"))
            {
                cursor += 2;
            }
            else if (IsKeyword(tokens, cursor, "update"))
            {
                cursor += 1;
            }
            else if (IsKeyword(tokens, cursor, "delete") && IsKeyword(tokens, cursor + 1, "from"))
            {
                cursor += 2;
            }
            else
            {
                continue;
            }

            if (!TryReadQualifiedName(tokens, ref cursor, out var parsedName))
                continue;

            targetTable = parsedName;
        }

        if (string.IsNullOrWhiteSpace(targetTable))
            return null;

        var tableName = NormalizeIdentifier(targetTable);
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

    private static bool IsKeyword(IReadOnlyList<SqlScanToken> tokens, int index, string keyword)
    {
        return index >= 0 &&
               index < tokens.Count &&
               tokens[index].Kind == SqlScanTokenKind.Word &&
               string.Equals(tokens[index].UpperText, keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadIdentifier(IReadOnlyList<SqlScanToken> tokens, ref int index, out string value)
    {
        value = string.Empty;
        if (index >= tokens.Count || !tokens[index].IsIdentifier)
            return false;

        value = NormalizeIdentifier(tokens[index].Text);
        index++;
        return true;
    }

    private static bool TryReadQualifiedName(IReadOnlyList<SqlScanToken> tokens, ref int index, out string value)
    {
        value = string.Empty;
        if (!TryReadIdentifier(tokens, ref index, out var first))
            return false;

        value = first;
        if (index + 1 < tokens.Count &&
            tokens[index].Kind == SqlScanTokenKind.Symbol &&
            tokens[index].Text == "." &&
            tokens[index + 1].IsIdentifier)
        {
            var second = NormalizeIdentifier(tokens[index + 1].Text);
            value = $"{first}.{second}";
            index += 2;
        }

        return true;
    }

    private static bool IsReservedAliasKeyword(string value)
    {
        return value.Equals("where", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("join", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("inner", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("left", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("right", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("full", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("cross", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("order", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("group", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("set", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("values", StringComparison.OrdinalIgnoreCase);
    }

    private static List<SqlScanToken> TokenizeSql(string text)
    {
        var tokens = new List<SqlScanToken>();
        var index = 0;

        while (index < text.Length)
        {
            var current = text[index];
            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (current == '-' && index + 1 < text.Length && text[index + 1] == '-')
            {
                index += 2;
                while (index < text.Length && text[index] != '\n')
                    index++;
                continue;
            }

            if (current == '/' && index + 1 < text.Length && text[index + 1] == '*')
            {
                index += 2;
                while (index + 1 < text.Length && !(text[index] == '*' && text[index + 1] == '/'))
                    index++;

                if (index + 1 < text.Length)
                    index += 2;

                continue;
            }

            if (current is '[' or '`' or '"')
            {
                var closing = current == '[' ? ']' : current;
                var start = index++;
                while (index < text.Length && text[index] != closing)
                    index++;

                if (index < text.Length)
                    index++;

                tokens.Add(new SqlScanToken(text[start..index], SqlScanTokenKind.Identifier, start, index));
                continue;
            }

            if (char.IsLetter(current) || current == '_')
            {
                var start = index++;
                while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] is '_' or '$'))
                    index++;

                tokens.Add(new SqlScanToken(text[start..index], SqlScanTokenKind.Word, start, index));
                continue;
            }

            tokens.Add(new SqlScanToken(text[index].ToString(), SqlScanTokenKind.Symbol, index, index + 1));
            index++;
        }

        return tokens;
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

    private static bool TryReadParenthesizedTokenRange(
        IReadOnlyList<SqlScanToken> tokens,
        ref int index,
        out int contentStart,
        out int contentEnd)
    {
        contentStart = 0;
        contentEnd = 0;

        if (index >= tokens.Count || tokens[index].Kind != SqlScanTokenKind.Symbol || tokens[index].Text != "(")
            return false;

        var depth = 1;
        contentStart = tokens[index].EndIndex;
        index++;

        while (index < tokens.Count)
        {
            if (tokens[index].Kind == SqlScanTokenKind.Symbol)
            {
                if (tokens[index].Text == "(")
                {
                    depth++;
                    index++;
                    continue;
                }

                if (tokens[index].Text == ")")
                {
                    depth--;
                    if (depth == 0)
                    {
                        contentEnd = tokens[index].StartIndex;
                        index++;
                        return true;
                    }
                }
            }

            index++;
        }

        return false;
    }

    private static string[] InferColumnsFromQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<string>();

        var tokens = TokenizeSql(query);
        var selectIndex = FindTopLevelKeyword(tokens, 0, "select");
        if (selectIndex < 0)
            return Array.Empty<string>();

        var fromIndex = FindTopLevelKeyword(tokens, selectIndex + 1, "from");
        var projectionTokens = fromIndex > selectIndex
            ? tokens.Skip(selectIndex + 1).Take(fromIndex - selectIndex - 1).ToArray()
            : tokens.Skip(selectIndex + 1).ToArray();

        return SplitTopLevel(projectionTokens)
            .Select(InferColumnName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int FindTopLevelKeyword(IReadOnlyList<SqlScanToken> tokens, int startIndex, string keyword)
    {
        var depth = 0;
        for (var i = startIndex; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == SqlScanTokenKind.Symbol)
            {
                if (tokens[i].Text == "(")
                {
                    depth++;
                    continue;
                }

                if (tokens[i].Text == ")")
                {
                    depth = Math.Max(0, depth - 1);
                    continue;
                }
            }

            if (depth == 0 && IsKeyword(tokens, i, keyword))
                return i;
        }

        return -1;
    }

    private static IEnumerable<IReadOnlyList<SqlScanToken>> SplitTopLevel(IReadOnlyList<SqlScanToken> tokens)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == SqlScanTokenKind.Symbol)
            {
                if (tokens[i].Text == "(")
                {
                    depth++;
                    continue;
                }

                if (tokens[i].Text == ")")
                {
                    depth = Math.Max(0, depth - 1);
                    continue;
                }

                if (tokens[i].Text == "," && depth == 0)
                {
                    yield return tokens.Skip(start).Take(i - start).ToArray();
                    start = i + 1;
                }
            }
        }

        if (start < tokens.Count)
            yield return tokens.Skip(start).ToArray();
    }

    private static string InferColumnName(IReadOnlyList<SqlScanToken> tokens)
    {
        if (tokens.Count == 0)
            return string.Empty;

        var depth = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == SqlScanTokenKind.Symbol)
            {
                if (tokens[i].Text == "(")
                {
                    depth++;
                    continue;
                }

                if (tokens[i].Text == ")")
                {
                    depth = Math.Max(0, depth - 1);
                    continue;
                }
            }

            if (depth == 0 && IsKeyword(tokens, i, "as") && i + 1 < tokens.Count && tokens[i + 1].IsIdentifier)
                return NormalizeIdentifier(tokens[i + 1].Text);
        }

        for (var i = tokens.Count - 1; i >= 1; i--)
        {
            if (tokens[i].IsIdentifier &&
                tokens[i - 1].Kind != SqlScanTokenKind.Symbol)
                return NormalizeIdentifier(tokens[i].Text);
        }

        for (var i = tokens.Count - 1; i >= 2; i--)
        {
            if (tokens[i].IsIdentifier &&
                tokens[i - 1].Kind == SqlScanTokenKind.Symbol &&
                tokens[i - 1].Text == ".")
                return NormalizeIdentifier(tokens[i].Text);
        }

        return tokens.LastOrDefault().IsIdentifier ? NormalizeIdentifier(tokens[^1].Text) : string.Empty;
    }

    private sealed class SchemaCompletionCache
    {
        public bool TablesLoaded { get; set; }
        public HashSet<string> Tables { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, SchemaNode> TableNodes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, ColumnCompletionInfo[]> ColumnsByTable { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> LoadedTables { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record ColumnCompletionInfo(string Name, string DataType, int Length, int Precision, int Scale);

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

    private enum SqlScanTokenKind
    {
        Word,
        Identifier,
        Symbol
    }

    private readonly record struct SqlScanToken(string Text, SqlScanTokenKind Kind, int StartIndex, int EndIndex)
    {
        public string UpperText => Text.ToUpperInvariant();
        public bool IsIdentifier => Kind is SqlScanTokenKind.Word or SqlScanTokenKind.Identifier;
    }

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
    Cte,
    Function
}

public sealed record SqlCompletionData(string Text, string Description, CompletionItemKind Kind, double Priority = 0, string? Detail = null) : ICompletionData
{
    public IImage? Image => null;
    public object Content => BuildContent();
    object ICompletionData.Description => Description;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        var insertionText = Kind == CompletionItemKind.Function &&
                            insertionRequestEventArgs is TextInputEventArgs { Text: "(" }
            ? $"{Text}("
            : Text;

        textArea.Document.Replace(completionSegment, insertionText);
    }

    private object BuildContent()
    {
        var iconFont = TryGetFont("MaterialDesignIcons");
        var monospace = TryGetFont("MonospaceFont");

        var icon = new Avalonia.Controls.TextBlock
        {
            Text = GetIconGlyph(),
            Foreground = GetForeground(),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 0, 8, 0)
        };
        if (iconFont is not null)
            icon.FontFamily = iconFont;

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
            Text = Detail ?? Description,
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
            CompletionItemKind.Function => Avalonia.Media.Brushes.Khaki,
            _ => Avalonia.Media.Brushes.White
        };
    }

    private string GetIconGlyph()
    {
        return Kind switch
        {
            CompletionItemKind.Table => "\U000F04EB",
            CompletionItemKind.Column => "\U000F08DF",
            CompletionItemKind.Cte => "\U000F01BC",
            CompletionItemKind.Function => "\U000F0295",
            _ => "\U000F09EE"
        };
    }
}
