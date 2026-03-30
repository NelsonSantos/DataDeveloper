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
    public SchemaExplorer(IDatabaseProvider databaseProvider, IConnectionSettings connectionSettings)
    {
        ConnectionSettings = connectionSettings;
        _databaseProvider = databaseProvider;
    }

    public IConnectionSettings ConnectionSettings { get; }
    public ObservableCollection<SchemaNode> RootConnections { get; private set; } = new();
    public async Task InitializeSchemaNode()
    {
        var connection = new SchemaNode(NodeType.Connection, ConnectionSettings.Name, isFolder: true, parent: null);
        
        var tables = new SchemaNode(NodeType.Tables, "Tables", isFolder: true, parent: connection);
        var tableNames = await this.GetTablesAsync();

        foreach (var tableName in tableNames)
        {
            var tableNode = new SchemaNode(NodeType.Table, tableName, isFolder: false, parent: tables);
            tableNode.Children.Add(new SchemaNode(NodeType.Columns, "Columns", isFolder: true, parent: tableNode, canLoad: true));
            tables.Children.Add( tableNode);
        }
        var views = new SchemaNode(NodeType.Views, "Views", isFolder: true, parent: connection);
        var viewNames = await GetViewsAsync();
        foreach (var viewName in viewNames)
        {
            var viewNode = new SchemaNode(NodeType.View, viewName, isFolder: false, parent: views);
            viewNode.Children.Add(new SchemaNode(NodeType.Columns, "Columns", isFolder: true, parent: viewNode, canLoad: true));
            views.Children.Add(viewNode);
        }

        var procedures = new SchemaNode(NodeType.Procedures, "Procedures", isFolder: true, parent: connection);
        var procedureNames = await GetProceduresAsync();
        foreach (var procedure in procedureNames)
        {
            var procedureNode = new SchemaNode(NodeType.Procedure, procedure.Name, isFolder: false, parent: procedures, details: null, tag: procedure);
            procedureNode.Children.Add(new SchemaNode(NodeType.Parameters, "Parameters", isFolder: true, parent: procedureNode, canLoad: true));
            procedures.Children.Add(procedureNode);
        }

        var functions = new SchemaNode(NodeType.Functions, "Functions", isFolder: true, parent: connection);
        var functionNames = await GetFunctionsAsync();
        foreach (var function in functionNames)
        {
            var functionDetails = string.IsNullOrWhiteSpace(function.DataType) ? null : function.DataType;
            var functionNode = new SchemaNode(NodeType.Function, function.Name, isFolder: false, parent: functions, details: functionDetails, tag: function);
            functionNode.Children.Add(new SchemaNode(NodeType.Parameters, "Parameters", isFolder: true, parent: functionNode, canLoad: true));
            functions.Children.Add(functionNode);
        }

        connection.Children.Add(tables);
        connection.Children.Add(views);
        connection.Children.Add(procedures);
        connection.Children.Add(functions);

        RootConnections = new ObservableCollection<SchemaNode> { connection };
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

        var parameters = new { tableName = table.NodeType == NodeType.Columns ? table.Parent?.Name : table.Name };
        await using var connection = _databaseProvider.GetConnection();
        var columns = await connection.QueryAsync<ColumnModel>(_databaseProvider.GetColumnStatement(), param: parameters, commandType: CommandType.Text);
        
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
