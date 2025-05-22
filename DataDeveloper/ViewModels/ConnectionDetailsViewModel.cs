using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Dapper;
using Dock.Model.ReactiveUI.Controls;
using DynamicData;
using Microsoft.Data.SqlClient;

namespace DataDeveloper.ViewModels;

public class ConnectionDetailsViewModel : Tool
{
    private SqlConnectionInfo _connection;

    public ConnectionDetailsViewModel()
    {
        _connection = new SqlConnectionInfo()
        {
            Database = "test-data-developer",
            Password = "Nass5544@",
            Server = "192.168.68.132",
            User = "sa",
            // Database = "NXGenMarketplace_Payment_Development",
            // Password = "123mudar",
            // Server = "192.168.239.48",
            // User = "user_app",
        };
        this.Initialization = LoadTables();
    }
    
    public Task Initialization { get; private set; }
    private async Task LoadTables()
    {
        try
        {
            await using var conn = new SqlConnection(_connection.ConnectionString);
            conn.Open();
            
            var tableInfoList = await conn.QueryAsync<TableInfo>("SELECT TABLE_NAME as TableName FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'", conn);

            TableNames.Clear();
            TableNames.AddRange(tableInfoList);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao carregar tabelas: " + ex.Message);
        }
    }
    
    public ObservableCollection<TableInfo> TableNames { get; } = new();
    
}

public class TableInfo : ViewModels.ViewModelBase
{
    public string TableName { get; set; }
}