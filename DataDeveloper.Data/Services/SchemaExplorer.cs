using System.Collections.ObjectModel;
using System.Data;
using Dapper;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Services;

public class SchemaExplorer : ISchemaExplorer
{
    private readonly IDatabaseProvider _databaseProvider;
    private readonly IProviderSqlAnalyzer _sqlAnalyzer;
    public SchemaExplorer(IDatabaseProvider databaseProvider, IConnectionSettings connectionSettings)
    {
        ConnectionSettings = connectionSettings;
        _databaseProvider = databaseProvider;
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

            connection.Children.Add(new SchemaNode(NodeType.Tables, "Tables", isFolder: true, parent: connection));
            connection.Children.Add(new SchemaNode(NodeType.Views, "Views", isFolder: true, parent: connection));
            if (ConnectionSettings.DatabaseType != DatabaseType.SqLite)
            {
                connection.Children.Add(new SchemaNode(NodeType.Procedures, "Procedures", isFolder: true, parent: connection));
                connection.Children.Add(new SchemaNode(NodeType.Functions, "Functions", isFolder: true, parent: connection));
            }

            RootConnections = new ObservableCollection<SchemaNode> { connection };
        }

        await RefreshFolderAsync(NodeType.Tables);
        await RefreshFolderAsync(NodeType.Views);
        if (ConnectionSettings.DatabaseType != DatabaseType.SqLite)
        {
            await RefreshFolderAsync(NodeType.Procedures);
            await RefreshFolderAsync(NodeType.Functions);
        }
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

        var folderType = target.ObjectType switch
        {
            SchemaObjectType.Table => NodeType.Tables,
            SchemaObjectType.View => NodeType.Views,
            SchemaObjectType.Procedure => NodeType.Procedures,
            SchemaObjectType.Function => NodeType.Functions,
            _ => NodeType.None
        };

        if (folderType == NodeType.None)
        {
            await RefreshSchemaAsync();
            return;
        }

        if (target.Action is SchemaRefreshAction.Create or SchemaRefreshAction.Drop || string.IsNullOrWhiteSpace(target.ObjectName))
        {
            await RefreshFolderAsync(folderType);
            return;
        }

        await RefreshObjectAsync(folderType, target.ObjectName!);
    }

    private async Task<IEnumerable<string>> GetTablesAsync()
    {
         await using var connection = _databaseProvider.GetConnection();
         var tables = await connection.QueryAsync<string>(_databaseProvider.GetTableStatement(), commandType: CommandType.Text);
         return tables.OrderBy(t => t);
    }

    private async Task<IEnumerable<string>> GetViewsAsync()
    {
        await using var connection = _databaseProvider.GetConnection();
        var views = await connection.QueryAsync<string>(_databaseProvider.GetViewStatement(), commandType: CommandType.Text);
        return views.OrderBy(v => v);
    }

    private async Task<IEnumerable<DatabaseObjectModel>> GetProceduresAsync()
    {
        await using var connection = _databaseProvider.GetConnection();
        var procedures = await connection.QueryAsync<DatabaseObjectModel>(_databaseProvider.GetProcedureStatement(), commandType: CommandType.Text);
        return procedures.OrderBy(p => p.Name);
    }

    private async Task<IEnumerable<DatabaseObjectModel>> GetFunctionsAsync()
    {
        await using var connection = _databaseProvider.GetConnection();
        var functions = await connection.QueryAsync<DatabaseObjectModel>(_databaseProvider.GetFunctionStatement(), commandType: CommandType.Text);
        return functions.OrderBy(f => f.Name);
    }
    
    public async Task LoadTableColumnsAsync(SchemaNode table)
    {
        var tableName = table.NodeType == NodeType.Columns ? table.Parent?.Name : table.Name;
        var columnStatement = _databaseProvider.GetColumnStatement();
        object? parameters = new { tableName = tableName };

        if (ConnectionSettings.DatabaseType == DatabaseType.SqLite && !string.IsNullOrWhiteSpace(tableName))
        {
            var escapedTableName = tableName.Replace("'", "''", StringComparison.Ordinal);
            columnStatement = columnStatement.Replace("__table_name__", $"'{escapedTableName}'", StringComparison.Ordinal);
            parameters = null;
        }

        await using var connection = _databaseProvider.GetConnection();
        var columns = await connection.QueryAsync<ColumnModel>(columnStatement, param: parameters, commandType: CommandType.Text);
        
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
        }
    }

    private async Task LoadRoutineParametersAsync(SchemaNode node)
    {
        var routineNode = node.Parent;
        if (routineNode?.Tag is not DatabaseObjectModel routine)
            return;

        var parameters = new { SpecificName = routine.SpecificName ?? routine.Name };
        await using var connection = _databaseProvider.GetConnection();
        var routineParameters = await connection.QueryAsync<RoutineParameterModel>(_databaseProvider.GetRoutineParameterStatement(), param: parameters, commandType: CommandType.Text);

        node.Children.Clear();
        foreach (var parameter in routineParameters)
        {
            var details = BuildParameterDetails(parameter);
            node.Children.Add(new SchemaNode(NodeType.Parameter, parameter.Name, isFolder: false, parent: node, details: details, tag: parameter));
        }

        node.CanLoad = false;
    }

    private async Task RefreshFolderAsync(NodeType folderType)
    {
        var folder = FindFolder(folderType);
        if (folder is null)
            return;

        switch (folderType)
        {
            case NodeType.Tables:
                await SyncObjectFolderAsync(folder, await GetTablesAsync(), NodeType.Table, null);
                break;
            case NodeType.Views:
                await SyncObjectFolderAsync(folder, await GetViewsAsync(), NodeType.View, null);
                break;
            case NodeType.Procedures:
                await SyncRoutineFolderAsync(folder, await GetProceduresAsync(), NodeType.Procedure);
                break;
            case NodeType.Functions:
                await SyncRoutineFolderAsync(folder, await GetFunctionsAsync(), NodeType.Function);
                break;
        }
    }

    private async Task RefreshObjectAsync(NodeType folderType, string objectName)
    {
        var folder = FindFolder(folderType);
        if (folder is null)
        {
            await RefreshSchemaAsync();
            return;
        }

        var normalizedTarget = NormalizeObjectName(objectName);
        var existingNode = folder.Children.FirstOrDefault(child => NormalizeObjectName(child.Name) == normalizedTarget);

        await RefreshFolderAsync(folderType);

        existingNode = folder.Children.FirstOrDefault(child => NormalizeObjectName(child.Name) == normalizedTarget);
        if (existingNode is null)
            return;

        switch (existingNode.NodeType)
        {
            case NodeType.Table:
            case NodeType.View:
                var columnsFolder = existingNode.Children.FirstOrDefault(child => child.NodeType == NodeType.Columns);
                if (columnsFolder is not null && !columnsFolder.CanLoad)
                    await LoadTableColumnsAsync(columnsFolder);
                break;
            case NodeType.Procedure:
            case NodeType.Function:
                var parametersFolder = existingNode.Children.FirstOrDefault(child => child.NodeType == NodeType.Parameters);
                if (parametersFolder is not null && !parametersFolder.CanLoad)
                    await LoadNodeAsync(parametersFolder);
                break;
        }
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

    private async Task SyncObjectFolderAsync(SchemaNode folder, IEnumerable<string> objectNames, NodeType nodeType, Func<string, string?>? detailsFactory)
    {
        var existing = folder.Children.ToDictionary(child => NormalizeObjectName(child.Name), child => child);
        var orderedNames = objectNames.OrderBy(name => name).ToList();
        var refreshedChildren = new List<SchemaNode>();

        foreach (var objectName in orderedNames)
        {
            var key = NormalizeObjectName(objectName);
            if (existing.TryGetValue(key, out var currentNode))
            {
                refreshedChildren.Add(currentNode);
                EnsureObjectChildren(currentNode);
                continue;
            }

            var node = new SchemaNode(nodeType, objectName, isFolder: false, parent: folder, details: detailsFactory?.Invoke(objectName));
            EnsureObjectChildren(node);
            refreshedChildren.Add(node);
        }

        ReplaceChildren(folder, refreshedChildren);
        await Task.CompletedTask;
    }

    private async Task SyncRoutineFolderAsync(SchemaNode folder, IEnumerable<DatabaseObjectModel> routines, NodeType nodeType)
    {
        var existing = folder.Children.ToDictionary(child => NormalizeObjectName(child.Name), child => child);
        var refreshedChildren = new List<SchemaNode>();

        foreach (var routine in routines.OrderBy(item => item.Name))
        {
            var key = NormalizeObjectName(routine.Name);
            if (existing.TryGetValue(key, out var currentNode))
            {
                refreshedChildren.Add(currentNode);
                EnsureObjectChildren(currentNode);
                continue;
            }

            var details = nodeType == NodeType.Function && !string.IsNullOrWhiteSpace(routine.DataType)
                ? routine.DataType
                : null;
            var node = new SchemaNode(nodeType, routine.Name, isFolder: false, parent: folder, details: details, tag: routine);
            EnsureObjectChildren(node);
            refreshedChildren.Add(node);
        }

        ReplaceChildren(folder, refreshedChildren);
        await Task.CompletedTask;
    }

    private static void EnsureObjectChildren(SchemaNode node)
    {
        switch (node.NodeType)
        {
            case NodeType.Table:
            case NodeType.View:
                if (!node.Children.Any(child => child.NodeType == NodeType.Columns))
                    node.Children.Add(new SchemaNode(NodeType.Columns, "Columns", isFolder: true, parent: node, canLoad: true));
                break;
            case NodeType.Procedure:
            case NodeType.Function:
                if (!node.Children.Any(child => child.NodeType == NodeType.Parameters))
                    node.Children.Add(new SchemaNode(NodeType.Parameters, "Parameters", isFolder: true, parent: node, canLoad: true));
                break;
        }
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
