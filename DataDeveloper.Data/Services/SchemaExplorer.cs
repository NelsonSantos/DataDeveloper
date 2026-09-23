using System.Collections.ObjectModel;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services.Metadata;
using DataDeveloper.Data.Services.SqlDialects;

namespace DataDeveloper.Data.Services;

public class SchemaExplorer : ISchemaExplorer
{
    private static readonly IReadOnlyDictionary<DbObjectKind, (NodeType Folder, string Label, NodeType Item)> FolderLayout =
        new Dictionary<DbObjectKind, (NodeType, string, NodeType)>
        {
            [DbObjectKind.Table] = (NodeType.Tables, "Tables", NodeType.Table),
            [DbObjectKind.View] = (NodeType.Views, "Views", NodeType.View),
            [DbObjectKind.Procedure] = (NodeType.Procedures, "Procedures", NodeType.Procedure),
            [DbObjectKind.Function] = (NodeType.Functions, "Functions", NodeType.Function)
        };

    private readonly SchemaMetadataService _metadata;
    private readonly IProviderSqlAnalyzer _sqlAnalyzer;

    public SchemaExplorer(IDatabaseProvider databaseProvider, IConnectionSettings connectionSettings)
        : this(databaseProvider, connectionSettings, catalog: null)
    {
    }

    /// <param name="catalog">Metadata SQL; defaults to the catalog of the settings' database type.</param>
    public SchemaExplorer(IDatabaseProvider databaseProvider, IConnectionSettings connectionSettings, IObjectCatalog? catalog)
    {
        ConnectionSettings = connectionSettings;
        _metadata = new SchemaMetadataService(connectionSettings, databaseProvider, catalog);
        _sqlAnalyzer = ProviderSqlAnalyzer.Create(connectionSettings.DatabaseType);
    }

    public IConnectionSettings ConnectionSettings { get; }
    public ObservableCollection<SchemaNode> RootConnections { get; private set; } = new();
    public async Task InitializeSchemaNode()
    {
        var connection = RootConnections.FirstOrDefault();
        if (connection is null)
        {
            connection = new SchemaNode(NodeType.Connection, BuildConnectionNodeName(), isFolder: true, parent: null)
            {
                IsExpanded = true
            };

            foreach (var kind in _metadata.RootObjectKinds)
            {
                var layout = FolderLayout[kind];
                connection.Children.Add(new SchemaNode(layout.Folder, layout.Label, isFolder: true, parent: connection));
            }

            RootConnections = new ObservableCollection<SchemaNode> { connection };
        }

        foreach (var kind in _metadata.RootObjectKinds)
            await RefreshFolderAsync(kind);
    }

    public async Task RefreshSchemaAsync()
    {
        await InitializeSchemaNode();
    }

    public async Task RefreshSchemaObjectAsync(string statement)
    {
        var target = _sqlAnalyzer.ParseSchemaRefreshTarget(statement);
        if (target is null || target.ObjectType == SchemaObjectType.Unknown)
        {
            await RefreshSchemaAsync();
            return;
        }

        DbObjectKind? kind = target.ObjectType switch
        {
            SchemaObjectType.Table => DbObjectKind.Table,
            SchemaObjectType.View => DbObjectKind.View,
            SchemaObjectType.Procedure => DbObjectKind.Procedure,
            SchemaObjectType.Function => DbObjectKind.Function,
            _ => null
        };

        if (kind is null)
        {
            await RefreshSchemaAsync();
            return;
        }

        if (target.Action is SchemaRefreshAction.Create or SchemaRefreshAction.Drop || string.IsNullOrWhiteSpace(target.ObjectName))
        {
            await RefreshFolderAsync(kind.Value);
            return;
        }

        await RefreshObjectAsync(kind.Value, target.ObjectName!);
    }

    public async Task LoadTableColumnsAsync(SchemaNode table)
    {
        var owner = table.NodeType == NodeType.Columns ? table.Parent : table;
        var dialect = SqlDialect.For(ConnectionSettings.DatabaseType);
        var tableRef = owner is null
            ? null
            : DbObjectRef.FromSchemaNode(owner, dialect) ?? DbObjectRef.Parse(DbObjectKind.Table, owner.Name, dialect);
        IReadOnlyList<ColumnModel> columns = tableRef is null ? [] : await _metadata.GetColumnsAsync(tableRef);

        table.Children.Clear();
        foreach (var column in columns)
        {
            var columnDetails = $"{(column.IsPrimaryKey ? "PK-" : "")}{column.DataType}";
            
            switch (column.DataType.ToLower())
            {
                case "varchar":
                case "nvarchar":
                case "char":
                    columnDetails += $" ({(column.Length == -1 ? "max" : column.Length)})";
                    break;
                
                case "int":
                case "bigint":
                case "numeric":
                case "real":
                case "smallint":
                case "tinyint":
                case "bit":
                    break;

                default:
                    if (column.DataType.Contains("date") || column.DataType.Contains("time"))
                        break;

                    if (column.Precision != 0)
                        columnDetails += $"({column.Precision}{(column.Scale != 0 ? $", {column.Scale}" : "")})";
                    break;
            }

            columnDetails+= $" {(column.IsNullable ? " - null" : " - not null")}";

            table.Children.Add(new SchemaNode(NodeType.Column, column.Name, isFolder: false, parent: table, details: columnDetails, tag: column));
        }

        table.CanLoad = false;
    }

    public async Task LoadNodeAsync(SchemaNode node)
    {
        switch (node.NodeType)
        {
            case NodeType.Columns:
                await LoadTableColumnsAsync(node);
                break;
            case NodeType.Parameters:
                await LoadRoutineParametersAsync(node);
                break;
            case NodeType.Keys:
                await LoadKeysAsync(node);
                break;
            case NodeType.Constraints:
                await LoadCheckConstraintsAsync(node);
                break;
            case NodeType.Indexes:
                await LoadIndexesAsync(node);
                break;
        }
    }

    private async Task LoadKeysAsync(SchemaNode folder)
    {
        if (GetOwnerTableRef(folder) is not { } table)
            return;

        var keys = await _metadata.GetTableKeysAsync(table);
        var children = new List<SchemaNode>();

        foreach (var primaryKey in keys.PrimaryKeyColumns.GroupBy(row => row.ConstraintName ?? string.Empty))
        {
            var columns = primaryKey.OrderBy(row => row.OrdinalPosition).Select(row => row.ColumnName);
            var name = string.IsNullOrWhiteSpace(primaryKey.Key) ? "PRIMARY KEY" : primaryKey.Key;
            children.Add(new SchemaNode(NodeType.PrimaryKey, name, isFolder: false, parent: folder, details: FormatColumnList(columns)));
        }

        foreach (var uniqueKey in keys.UniqueConstraintColumns.GroupBy(row => row.ConstraintName).OrderBy(group => group.Key))
        {
            var columns = uniqueKey.OrderBy(row => row.OrdinalPosition).Select(row => row.ColumnName);
            children.Add(new SchemaNode(NodeType.UniqueKey, uniqueKey.Key, isFolder: false, parent: folder, details: FormatColumnList(columns)));
        }

        foreach (var foreignKey in keys.ForeignKeyColumns.GroupBy(row => row.ConstraintName).OrderBy(group => group.Key))
        {
            var rows = foreignKey.OrderBy(row => row.OrdinalPosition).ToList();
            var referencedSchema = rows[0].ReferencedSchemaName;
            var referencedTable = string.IsNullOrWhiteSpace(referencedSchema) ||
                                  string.Equals(referencedSchema, table.Schema, StringComparison.OrdinalIgnoreCase)
                ? rows[0].ReferencedTableName
                : $"{referencedSchema}.{rows[0].ReferencedTableName}";
            var details = $"{FormatColumnList(rows.Select(row => row.ColumnName))} → {referencedTable} {FormatColumnList(rows.Select(row => row.ReferencedColumnName))}";
            children.Add(new SchemaNode(NodeType.ForeignKey, foreignKey.Key, isFolder: false, parent: folder, details: details));
        }

        ReplaceChildren(folder, children);
        folder.CanLoad = false;
    }

    private async Task LoadCheckConstraintsAsync(SchemaNode folder)
    {
        if (GetOwnerTableRef(folder) is not { } table)
            return;

        var checks = await _metadata.GetCheckConstraintsAsync(table);
        var children = checks
            .Select(check => new SchemaNode(
                NodeType.CheckConstraint,
                string.IsNullOrWhiteSpace(check.ConstraintName) ? "CHECK" : check.ConstraintName,
                isFolder: false,
                parent: folder,
                details: check.Definition))
            .ToList();

        ReplaceChildren(folder, children);
        folder.CanLoad = false;
    }

    private async Task LoadIndexesAsync(SchemaNode folder)
    {
        if (GetOwnerTableRef(folder) is not { } table)
            return;

        var indexColumns = await _metadata.GetIndexesAsync(table);
        var children = indexColumns
            .GroupBy(row => row.IndexName)
            .OrderBy(group => group.Key)
            .Select(index =>
            {
                var rows = index.OrderBy(row => row.OrdinalPosition).ToList();
                var columns = FormatColumnList(rows.Select(row => row.IsDescending ? $"{row.ColumnName} desc" : row.ColumnName));
                var details = rows[0].IsUnique ? $"unique {columns}" : columns;
                return new SchemaNode(NodeType.Index, index.Key, isFolder: false, parent: folder, details: details);
            })
            .ToList();

        ReplaceChildren(folder, children);
        folder.CanLoad = false;
    }

    private DbObjectRef? GetOwnerTableRef(SchemaNode folder)
    {
        return folder.Parent is null
            ? null
            : DbObjectRef.FromSchemaNode(folder.Parent, SqlDialect.For(ConnectionSettings.DatabaseType));
    }

    private static string FormatColumnList(IEnumerable<string> columns)
    {
        return $"({string.Join(", ", columns)})";
    }

    private async Task LoadRoutineParametersAsync(SchemaNode node)
    {
        var routineNode = node.Parent;
        if (routineNode?.Tag is not DatabaseObjectModel routine)
            return;

        var routineParameters = await _metadata.GetRoutineParametersAsync(routine);

        node.Children.Clear();
        foreach (var parameter in routineParameters)
        {
            var details = BuildParameterDetails(parameter);
            node.Children.Add(new SchemaNode(NodeType.Parameter, parameter.Name, isFolder: false, parent: node, details: details, tag: parameter));
        }

        node.CanLoad = false;
    }

    private async Task RefreshFolderAsync(DbObjectKind kind)
    {
        var layout = FolderLayout[kind];
        var folder = FindFolder(layout.Folder);
        if (folder is null)
            return;

        var objects = await _metadata.ListObjectsAsync(kind);
        SyncObjectFolder(folder, kind, layout.Item, objects);
    }

    private async Task RefreshObjectAsync(DbObjectKind kind, string objectName)
    {
        var folder = FindFolder(FolderLayout[kind].Folder);
        if (folder is null)
        {
            await RefreshSchemaAsync();
            return;
        }

        await RefreshFolderAsync(kind);

        var normalizedTarget = NormalizeObjectName(objectName);
        var existingNode = folder.Children.FirstOrDefault(child => MatchesObjectName(child, normalizedTarget));
        if (existingNode is null)
            return;

        switch (existingNode.NodeType)
        {
            case NodeType.Table:
            case NodeType.View:
                // Reload only the detail folders the user already opened.
                foreach (var detailFolder in existingNode.Children.Where(child => child.IsFolder && !child.CanLoad).ToList())
                    await LoadNodeAsync(detailFolder);
                break;
            case NodeType.Procedure:
            case NodeType.Function:
                var parametersFolder = existingNode.Children.FirstOrDefault(child => child.NodeType == NodeType.Parameters);
                if (parametersFolder is not null && !parametersFolder.CanLoad)
                    await LoadNodeAsync(parametersFolder);
                break;
        }
    }

    // DDL may name an object with its schema even when the tree shows it unqualified.
    private static bool MatchesObjectName(SchemaNode node, string normalizedName)
    {
        return NormalizeObjectName(node.Name) == normalizedName ||
               (node.ObjectRef is not null && NormalizeObjectName(node.ObjectRef.QualifiedName) == normalizedName);
    }

    private SchemaNode? FindFolder(NodeType folderType)
    {
        var connection = RootConnections.FirstOrDefault();
        return connection?.Children.FirstOrDefault(child => child.NodeType == folderType);
    }

    private string BuildConnectionNodeName()
    {
        var databaseName = ConnectionSettings switch
        {
            DataDeveloper.Data.Providers.SqlServer.SqlServerConnectionSettings sqlServer => sqlServer.Database,
            DataDeveloper.Data.Providers.MySql.MySqlConnectionSettings mySql => mySql.Database,
            DataDeveloper.Data.Providers.PostgresSql.PostgresConnectionSettings postgres => postgres.Database,
            DataDeveloper.Data.Providers.Oracle.OracleConnectionSettings oracle => oracle.Database,
            DataDeveloper.Data.Providers.SqLite.SqLiteConnectionSettings sqLite => sqLite.Database,
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(databaseName)
            ? ConnectionSettings.Name
            : $"{ConnectionSettings.Name} ({databaseName})";
    }

    private static void SyncObjectFolder(SchemaNode folder, DbObjectKind kind, NodeType nodeType, IEnumerable<DatabaseObjectModel> objects)
    {
        var existing = folder.Children.ToDictionary(child => NormalizeObjectName(child.Name), child => child);
        var isRoutine = kind is DbObjectKind.Procedure or DbObjectKind.Function;
        var refreshedChildren = new List<SchemaNode>();

        // Objects in the default schema first, then the other schemas' objects grouped by schema.
        var orderedObjects = objects
            .OrderBy(item => item.IsDefaultSchema ? 0 : 1)
            .ThenBy(item => item.SchemaName)
            .ThenBy(item => item.Name);

        foreach (var item in orderedObjects)
        {
            var objectRef = new DbObjectRef(kind, string.IsNullOrWhiteSpace(item.SchemaName) ? null : item.SchemaName, item.Name);
            if (existing.TryGetValue(NormalizeObjectName(item.DisplayName), out var currentNode))
            {
                currentNode.ObjectRef = objectRef;
                refreshedChildren.Add(currentNode);
                EnsureObjectChildren(currentNode);
                continue;
            }

            var details = nodeType == NodeType.Function && !string.IsNullOrWhiteSpace(item.DataType)
                ? item.DataType
                : null;
            var node = new SchemaNode(nodeType, item.DisplayName, isFolder: false, parent: folder, details: details, tag: isRoutine ? item : null)
            {
                ObjectRef = objectRef
            };
            EnsureObjectChildren(node);
            refreshedChildren.Add(node);
        }

        ReplaceChildren(folder, refreshedChildren);
    }

    private static void EnsureObjectChildren(SchemaNode node)
    {
        switch (node.NodeType)
        {
            case NodeType.Table:
                AddFolderIfMissing(node, NodeType.Columns, "Columns");
                AddFolderIfMissing(node, NodeType.Keys, "Keys");
                AddFolderIfMissing(node, NodeType.Constraints, "Constraints");
                AddFolderIfMissing(node, NodeType.Indexes, "Indexes");
                break;
            case NodeType.View:
                AddFolderIfMissing(node, NodeType.Columns, "Columns");
                break;
            case NodeType.Procedure:
            case NodeType.Function:
                AddFolderIfMissing(node, NodeType.Parameters, "Parameters");
                break;
        }
    }

    private static void AddFolderIfMissing(SchemaNode node, NodeType folderType, string label)
    {
        if (!node.Children.Any(child => child.NodeType == folderType))
            node.Children.Add(new SchemaNode(folderType, label, isFolder: true, parent: node, canLoad: true));
    }

    private static void ReplaceChildren(SchemaNode folder, IReadOnlyList<SchemaNode> nodes)
    {
        folder.Children.Clear();
        foreach (var node in nodes)
            folder.Children.Add(node);
    }

    private static string NormalizeObjectName(string objectName)
    {
        return objectName
            .Trim()
            .Trim('[', ']', '`', '"')
            .ToLowerInvariant();
    }

    private static string BuildParameterDetails(RoutineParameterModel parameter)
    {
        var details = string.IsNullOrWhiteSpace(parameter.Mode)
            ? parameter.DataType
            : $"{parameter.Mode.ToLowerInvariant()} - {parameter.DataType}";

        switch (parameter.DataType.ToLowerInvariant())
        {
            case "varchar":
            case "nvarchar":
            case "char":
            case "nchar":
            case "varbinary":
            case "binary":
                if (parameter.Length != 0)
                    details += $" ({(parameter.Length == -1 ? "max" : parameter.Length)})";
                break;
            default:
                if (parameter.Precision != 0)
                    details += $" ({parameter.Precision}{(parameter.Scale != 0 ? $", {parameter.Scale}" : "")})";
                break;
        }

        details += parameter.IsNullable ? " - null" : " - not null";
        return details;
    }
}
