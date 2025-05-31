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
    public ObservableCollection<SchemaNode> RootConnections { get; private set; }
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
        
        connection.Children.Add(tables);

        RootConnections = new ObservableCollection<SchemaNode> { connection };
    }

    private async Task<IEnumerable<string>> GetTablesAsync()
    {
         using var connection = _databaseProvider.GetConnection();
         var tables = await connection.QueryAsync<string>(_databaseProvider.GetTableStatement(), commandType: CommandType.Text);
         return tables;
    }
    
    public async Task LoadTableColumnsAsync(SchemaNode table)
    {

        var parameters = new { tableName = table.NodeType == NodeType.Columns ? table.Parent?.Name : table.Name };
        using var connection = _databaseProvider.GetConnection();
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
}