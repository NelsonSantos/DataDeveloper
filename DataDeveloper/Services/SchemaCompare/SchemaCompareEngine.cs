using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Models.SchemaCompare;
using DataDeveloper.Data.Models.TableDesigner;
using DataDeveloper.Data.Services.SchemaCompare;
using DataDeveloper.Data.Services.TableDesigner;

namespace DataDeveloper.Services.SchemaCompare;

/// <summary>
/// Compares Tables/Views/Procedures/Functions between two connections of the SAME
/// <see cref="DatabaseType"/> and builds a combined SQL script that brings the destination
/// in line with the source. The script is always returned for manual review — this engine
/// never executes anything against either connection beyond read-only schema introspection.
///
/// Known v1 limitations:
///  - Same-DatabaseType pairs only; no cross-provider type mapping.
///  - No triggers (no infrastructure exists for them yet).
///  - Fixed category order (Tables -&gt; Views -&gt; Functions -&gt; Procedures); only intra-Tables FK
///    ordering is computed via <see cref="NewTableDependencyOrderer"/> - no cross-category graph.
///  - No rename detection across connections (a renamed table shows as New + OnlyInDestination).
///  - View/Procedure/Function diffing is DDL-text comparison (whitespace-normalized, otherwise
///    literal), not semantic - purely cosmetic differences beyond whitespace can still register
///    as Changed.
///  - Whole-object inclusion only - no per-column granularity within a Changed table's ALTER.
///  - Single default/current schema per connection, matching the rest of the app.
///  - Only-in-destination DROPs aren't FK-ordered or CASCADE-aware.
///  - An included New table can silently reference an excluded New table's FK - not detected.
/// </summary>
public static class SchemaCompareEngine
{
    private static readonly Dictionary<NodeType, SchemaCompareObjectType> FolderTypeMap = new()
    {
        [NodeType.Tables] = SchemaCompareObjectType.Table,
        [NodeType.Views] = SchemaCompareObjectType.View,
        [NodeType.Procedures] = SchemaCompareObjectType.Procedure,
        [NodeType.Functions] = SchemaCompareObjectType.Function
    };

    public static async Task<IReadOnlyList<SchemaCompareObjectResult>> CompareAsync(
        IConnectionSettings sourceConnectionSettings,
        IConnectionSettings destinationConnectionSettings,
        IReadOnlyList<SchemaCompareObjectRef> selectedSourceObjects,
        IProgress<(int Completed, int Total, string CurrentObjectName)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sourceExplorer = sourceConnectionSettings.GetSchemaExplorer();
        await sourceExplorer.InitializeSchemaNode();
        var sourceLookup = BuildObjectLookup(sourceExplorer);

        var destinationExplorer = destinationConnectionSettings.GetSchemaExplorer();
        await destinationExplorer.InitializeSchemaNode();
        var destinationLookup = BuildObjectLookup(destinationExplorer);

        // Table enumeration doesn't return schema-qualified names for every provider (see class
        // remarks), so a table's own schema comes back empty unless it lives outside the
        // connection's default schema. Resolving the default schema once up front lets generated
        // CREATE/ALTER scripts still show a schema-qualified name for the common case.
        var sourceDefaultSchema = await ResolveDefaultSchemaAsync(sourceConnectionSettings);
        var destinationDefaultSchema = await ResolveDefaultSchemaAsync(destinationConnectionSettings);

        var results = new List<SchemaCompareObjectResult>();

        for (var i = 0; i < selectedSourceObjects.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var objectRef = selectedSourceObjects[i];
            try
            {
                var result = objectRef.ObjectType == SchemaCompareObjectType.Table
                    ? await CompareTableAsync(sourceConnectionSettings, destinationConnectionSettings, sourceExplorer, destinationExplorer, sourceLookup, destinationLookup, objectRef, sourceDefaultSchema, destinationDefaultSchema)
                    : await CompareRoutineAsync(sourceConnectionSettings, destinationConnectionSettings, sourceLookup, destinationLookup, objectRef);
                results.Add(result);
            }
            catch (Exception ex)
            {
                results.Add(BuildErrorResult(objectRef, ex.Message));
            }

            progress?.Report((i + 1, selectedSourceObjects.Count, objectRef.Name));
        }

        results.AddRange(BuildOnlyInDestinationResults(sourceLookup, destinationLookup, destinationConnectionSettings.DatabaseType));

        return results;
    }

    /// <summary>
    /// Executes a previously generated script against the destination connection. Wrapped in a
    /// transaction where the provider supports transactional DDL (MySQL/Oracle DDL auto-commits
    /// regardless, so there is nothing to roll back there). Stops at the first failing statement,
    /// matching every other execution path in this codebase (no partial-success reporting).
    /// </summary>
    public static async Task ExecuteScriptAsync(IConnectionSettings destinationConnectionSettings, string script, CancellationToken cancellationToken = default)
    {
        var executor = destinationConnectionSettings.GetStatementExecutor();
        var supportsTransactionalDdl = destinationConnectionSettings.DatabaseType
            is DatabaseType.SqlServer or DatabaseType.PostgresSql or DatabaseType.SqLite;

        if (supportsTransactionalDdl)
            await executor.BeginTransaction(cancellationToken);

        try
        {
            await executor.ExecuteStatement(script, cancellationToken: cancellationToken);
            if (supportsTransactionalDdl)
                await executor.CommitTransaction(cancellationToken);
        }
        catch
        {
            if (supportsTransactionalDdl)
                await executor.RollbackTransaction(cancellationToken);
            throw;
        }
    }

    public static string BuildFinalScript(
        string sourceConnectionName,
        string destinationConnectionName,
        DatabaseType destinationDatabaseType,
        IReadOnlyList<SchemaCompareObjectResult> resultsToInclude)
    {
        var tables = resultsToInclude.Where(r => r.ObjectType == SchemaCompareObjectType.Table).ToList();
        var views = resultsToInclude.Where(r => r.ObjectType == SchemaCompareObjectType.View).ToList();
        var functions = resultsToInclude.Where(r => r.ObjectType == SchemaCompareObjectType.Function).ToList();
        var procedures = resultsToInclude.Where(r => r.ObjectType == SchemaCompareObjectType.Procedure).ToList();

        var newTables = NewTableDependencyOrderer.Order(tables.Where(r => r.Status == SchemaCompareResultStatus.New).ToList());
        var changedTables = tables.Where(r => r.Status == SchemaCompareResultStatus.Changed);
        var droppedTables = tables.Where(r => r.Status == SchemaCompareResultStatus.OnlyInDestination);
        var orderedTables = newTables.Concat(changedTables).Concat(droppedTables);

        var orderedResults = orderedTables
            .Concat(OrderByStatus(views))
            .Concat(OrderByStatus(functions))
            .Concat(OrderByStatus(procedures));

        var blocks = orderedResults
            .Where(r => !string.IsNullOrWhiteSpace(r.Script))
            .Select(r => $"-- ==== {r.ObjectType} {r.Name} ({r.Status}) ===={Environment.NewLine}{r.Script}")
            .ToList();

        var banner =
            $"-- Schema Diff Script{Environment.NewLine}" +
            $"-- Source: {sourceConnectionName}{Environment.NewLine}" +
            $"-- Destination: {destinationConnectionName}{Environment.NewLine}" +
            $"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC{Environment.NewLine}" +
            $"-- Review carefully before executing. Nothing has been run against the destination.{Environment.NewLine}{Environment.NewLine}";

        return blocks.Count == 0
            ? banner + "-- No changes selected."
            : banner + string.Join(BuildBlockSeparator(destinationDatabaseType), blocks);
    }

    /// <summary>
    /// CREATE FUNCTION/PROCEDURE/VIEW must be the first statement in a batch, and routine bodies
    /// commonly have no trailing delimiter of their own - without an explicit batch separator
    /// between blocks, StatementSplitter keeps accumulating tokens past a routine's own END and
    /// merges it with the next block, which is what caused "must be the first statement in a
    /// batch" and "must declare the scalar variable" errors on execution.
    /// </summary>
    private static string BuildBlockSeparator(DatabaseType databaseType)
    {
        var batchKeyword = databaseType switch
        {
            DatabaseType.SqlServer => "GO",
            DatabaseType.Oracle => "/",
            _ => null
        };

        return batchKeyword is null
            ? $"{Environment.NewLine}{Environment.NewLine}"
            : $"{Environment.NewLine}{Environment.NewLine}{batchKeyword}{Environment.NewLine}{Environment.NewLine}";
    }

    private static IEnumerable<SchemaCompareObjectResult> OrderByStatus(IReadOnlyList<SchemaCompareObjectResult> results)
    {
        return results.Where(r => r.Status == SchemaCompareResultStatus.New)
            .Concat(results.Where(r => r.Status == SchemaCompareResultStatus.Changed))
            .Concat(results.Where(r => r.Status == SchemaCompareResultStatus.OnlyInDestination));
    }

    private static async Task<SchemaCompareObjectResult> CompareTableAsync(
        IConnectionSettings sourceConnectionSettings,
        IConnectionSettings destinationConnectionSettings,
        ISchemaExplorer sourceExplorer,
        ISchemaExplorer destinationExplorer,
        Dictionary<SchemaCompareObjectType, Dictionary<string, SchemaNode>> sourceLookup,
        Dictionary<SchemaCompareObjectType, Dictionary<string, SchemaNode>> destinationLookup,
        SchemaCompareObjectRef objectRef,
        string sourceDefaultSchema,
        string destinationDefaultSchema)
    {
        var normalizedName = SchemaCompareObjectNameMatcher.Normalize(objectRef.Name);
        if (!TryGetNode(sourceLookup, SchemaCompareObjectType.Table, normalizedName, out var sourceNode))
            return BuildErrorResult(objectRef, "Object no longer exists on the source connection.");

        var sourceTableDefinition = await LoadTableDefinitionAsync(sourceConnectionSettings, sourceExplorer, sourceNode!, sourceDefaultSchema);

        if (!TryGetNode(destinationLookup, SchemaCompareObjectType.Table, normalizedName, out var destinationNode))
        {
            // Replicate the source's own schema on the destination (falls back to the
            // destination's default schema only if the source's couldn't be resolved either).
            if (string.IsNullOrEmpty(sourceTableDefinition.SchemaName))
                sourceTableDefinition.SchemaName = destinationDefaultSchema;

            var createScript = TableDdlScriptBuilder.BuildCreateTableScript(destinationConnectionSettings.DatabaseType, sourceTableDefinition);
            return new SchemaCompareObjectResult
            {
                ObjectType = SchemaCompareObjectType.Table,
                Name = objectRef.Name,
                Status = SchemaCompareResultStatus.New,
                Script = createScript,
                IsIncludedByDefault = true,
                NewTableDefinition = sourceTableDefinition
            };
        }

        var destinationTableDefinition = await LoadTableDefinitionAsync(destinationConnectionSettings, destinationExplorer, destinationNode!, destinationDefaultSchema);
        var alterScript = TableDdlScriptBuilder.BuildAlterTableScript(destinationConnectionSettings.DatabaseType, destinationTableDefinition, sourceTableDefinition);

        return string.IsNullOrWhiteSpace(alterScript)
            ? new SchemaCompareObjectResult { ObjectType = SchemaCompareObjectType.Table, Name = objectRef.Name, Status = SchemaCompareResultStatus.Unchanged, IsIncludedByDefault = false }
            : new SchemaCompareObjectResult { ObjectType = SchemaCompareObjectType.Table, Name = objectRef.Name, Status = SchemaCompareResultStatus.Changed, Script = alterScript, IsIncludedByDefault = true };
    }

    private static async Task<TableDefinition> LoadTableDefinitionAsync(IConnectionSettings connectionSettings, ISchemaExplorer explorer, SchemaNode tableNode, string defaultSchema)
    {
        var columnsFolder = tableNode.Children.FirstOrDefault(child => child.NodeType == NodeType.Columns);
        if (columnsFolder is not null && columnsFolder.CanLoad)
            await explorer.LoadNodeAsync(columnsFolder);

        var loadedColumns = (columnsFolder?.Children ?? Enumerable.Empty<SchemaNode>())
            .Where(child => child.NodeType == NodeType.Column && child.Tag is ColumnModel)
            .Select(child => (ColumnModel)child.Tag!)
            .ToList();

        var (schemaName, tableName) = SplitObjectName(tableNode.Name);
        if (string.IsNullOrEmpty(schemaName))
            schemaName = defaultSchema;

        return await TableDefinitionLoader.LoadAsync(connectionSettings, schemaName, tableName, loadedColumns);
    }

    /// <summary>
    /// Table (and view) enumeration doesn't return a schema-qualified name for every provider -
    /// SQL Server and Postgres both list bare table names (Postgres already scoped to
    /// <c>current_schema()</c>; SQL Server lists across all visible schemas with no filter at
    /// all). Resolving the connection's own default schema lets generated scripts still show a
    /// schema-qualified name for the common case where the table lives in that default schema.
    /// Not attempted for MySQL/Oracle/SQLite, where this gap doesn't apply the same way.
    /// </summary>
    private static async Task<string> ResolveDefaultSchemaAsync(IConnectionSettings connectionSettings)
    {
        var query = connectionSettings.DatabaseType switch
        {
            DatabaseType.SqlServer => "select schema_name();",
            DatabaseType.PostgresSql => "select current_schema();",
            _ => null
        };

        if (query is null)
            return string.Empty;

        try
        {
            var results = await connectionSettings.GetStatementExecutor().ExecuteStatement(query);
            var result = results.First();
            if (!result.HasDataReader)
                return string.Empty;

            try
            {
                if (await result.DataReader!.ReadAsync() && !await result.DataReader.IsDBNullAsync(0))
                    return result.DataReader.GetString(0);

                return string.Empty;
            }
            finally
            {
                await result.CloseDataReader();
            }
        }
        catch
        {
            // Best-effort only: an unqualified script (no schema prefix) is still valid DDL and
            // simply lands in the destination connection's own default schema.
            return string.Empty;
        }
    }

    private static async Task<SchemaCompareObjectResult> CompareRoutineAsync(
        IConnectionSettings sourceConnectionSettings,
        IConnectionSettings destinationConnectionSettings,
        Dictionary<SchemaCompareObjectType, Dictionary<string, SchemaNode>> sourceLookup,
        Dictionary<SchemaCompareObjectType, Dictionary<string, SchemaNode>> destinationLookup,
        SchemaCompareObjectRef objectRef)
    {
        var normalizedName = SchemaCompareObjectNameMatcher.Normalize(objectRef.Name);
        if (!TryGetNode(sourceLookup, objectRef.ObjectType, normalizedName, out var sourceNode))
            return BuildErrorResult(objectRef, "Object no longer exists on the source connection.");

        var sourceDdl = await RoutineDdlRetriever.GetDdlAsync(sourceConnectionSettings, sourceNode!);

        if (!TryGetNode(destinationLookup, objectRef.ObjectType, normalizedName, out var destinationNode))
        {
            return new SchemaCompareObjectResult
            {
                ObjectType = objectRef.ObjectType,
                Name = objectRef.Name,
                Status = SchemaCompareResultStatus.New,
                Script = sourceDdl,
                IsIncludedByDefault = true
            };
        }

        var destinationDdl = await RoutineDdlRetriever.GetDdlAsync(destinationConnectionSettings, destinationNode!);

        if (NormalizeWhitespace(sourceDdl) == NormalizeWhitespace(destinationDdl))
            return new SchemaCompareObjectResult { ObjectType = objectRef.ObjectType, Name = objectRef.Name, Status = SchemaCompareResultStatus.Unchanged, IsIncludedByDefault = false };

        var qualifiedName = DatabaseObjectScriptBuilder.BuildQualifiedName(destinationConnectionSettings, destinationNode!);
        var changedScript = CreateOrReplaceScriptRewriter.BuildChangedObjectScript(destinationConnectionSettings.DatabaseType, objectRef.ObjectType, qualifiedName, sourceDdl);

        return new SchemaCompareObjectResult { ObjectType = objectRef.ObjectType, Name = objectRef.Name, Status = SchemaCompareResultStatus.Changed, Script = changedScript, IsIncludedByDefault = true };
    }

    /// <summary>
    /// A destination object is "only in destination" when it has no counterpart anywhere in the
    /// source connection's full object list - not merely when it wasn't selected this run. An
    /// object that exists on both sides but was left unchecked is simply skipped (it's not a
    /// surprise; the user just chose not to compare it), so this checks against the full
    /// <paramref name="sourceLookup"/> rather than the user's selection.
    /// </summary>
    private static IEnumerable<SchemaCompareObjectResult> BuildOnlyInDestinationResults(
        Dictionary<SchemaCompareObjectType, Dictionary<string, SchemaNode>> sourceLookup,
        Dictionary<SchemaCompareObjectType, Dictionary<string, SchemaNode>> destinationLookup,
        DatabaseType destinationDatabaseType)
    {
        var results = new List<SchemaCompareObjectResult>();
        foreach (var (objectType, nodesByName) in destinationLookup)
        {
            var sourceNames = sourceLookup.TryGetValue(objectType, out var sourceByName)
                ? sourceByName.Keys
                : Enumerable.Empty<string>();
            var sourceNameSet = new HashSet<string>(sourceNames);

            foreach (var (normalizedName, node) in nodesByName)
            {
                if (sourceNameSet.Contains(normalizedName))
                    continue;

                results.Add(new SchemaCompareObjectResult
                {
                    ObjectType = objectType,
                    Name = node.Name,
                    Status = SchemaCompareResultStatus.OnlyInDestination,
                    Script = BuildDropScript(destinationDatabaseType, objectType, node.Name),
                    IsIncludedByDefault = false
                });
            }
        }

        return results;
    }

    private static string BuildDropScript(DatabaseType databaseType, SchemaCompareObjectType objectType, string objectName)
    {
        var kind = objectType switch
        {
            SchemaCompareObjectType.Table => "table",
            SchemaCompareObjectType.View => "view",
            SchemaCompareObjectType.Procedure => "procedure",
            SchemaCompareObjectType.Function => "function",
            _ => "object"
        };

        return $"drop {kind} {objectName};";
    }

    private static Dictionary<SchemaCompareObjectType, Dictionary<string, SchemaNode>> BuildObjectLookup(ISchemaExplorer explorer)
    {
        var lookup = new Dictionary<SchemaCompareObjectType, Dictionary<string, SchemaNode>>();
        var connectionNode = explorer.RootConnections.FirstOrDefault();
        if (connectionNode is null)
            return lookup;

        foreach (var folder in connectionNode.Children)
        {
            if (!FolderTypeMap.TryGetValue(folder.NodeType, out var objectType))
                continue;

            var byName = new Dictionary<string, SchemaNode>();
            foreach (var node in folder.Children)
                byName[SchemaCompareObjectNameMatcher.Normalize(node.Name)] = node;

            lookup[objectType] = byName;
        }

        return lookup;
    }

    private static bool TryGetNode(
        Dictionary<SchemaCompareObjectType, Dictionary<string, SchemaNode>> lookup,
        SchemaCompareObjectType objectType,
        string normalizedName,
        out SchemaNode? node)
    {
        node = null;
        return lookup.TryGetValue(objectType, out var byName) && byName.TryGetValue(normalizedName, out node);
    }

    private static SchemaCompareObjectResult BuildErrorResult(SchemaCompareObjectRef objectRef, string message)
    {
        return new SchemaCompareObjectResult
        {
            ObjectType = objectRef.ObjectType,
            Name = objectRef.Name,
            Status = SchemaCompareResultStatus.Error,
            ErrorMessage = message,
            IsIncludedByDefault = false
        };
    }

    private static (string SchemaName, string TableName) SplitObjectName(string objectName)
    {
        var parts = objectName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 1
            ? (string.Join(".", parts.Take(parts.Length - 1)), parts[^1])
            : (string.Empty, objectName);
    }

    private static string NormalizeWhitespace(string text)
    {
        return Regex.Replace(text.Trim(), @"\s+", " ");
    }
}
