using Dock.Model.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Reactive;
using System.Reflection.Metadata;
using System.Windows.Input;
using Avalonia.Controls;
using Dapper;
using DataDeveloper.Views;
using Microsoft.Data.SqlClient;
using ReactiveUI;

namespace DataDeveloper.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    
    public IDockable Layout { get; set; }
    
    public ObservableCollection<string> TableNames { get; } = new();
    
    public ReactiveCommand<Unit, Unit> ExecuteCommand { get; }
    
    private SqlConnectionInfo _connection;

    public MainWindowViewModel()
    {
        // Mostrar diálogo de conexão ao iniciar
        // var dialog = new ConnectionDialogViewModel();
        // dialog.OpenDialog();
        //
        // if (dialog.ConnectionInfo == null)
        // {
        //     Environment.Exit(0);
        //     return;
        // }
    
        //_connection = dialog.ConnectionInfo;
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
        LoadTables();

        ExecuteCommand = ReactiveCommand.Create(ExecuteQuery);

        CreateLayout();

        this.NewWindowCommand = ReactiveCommand.Create(() =>
        {
            var newWindow = new MainWindow();
            newWindow.Show();
        });
    }
    
    private void CreateLayout()
    {
        var factory = new DockFactory();
        Layout = factory.CreateLayout();
        if (Layout is { })
        {
            factory.InitLayout(Layout);
        }
    }    
    
    private void LoadTables()
    {
        try
        {
            using var conn = new SqlConnection(_connection.ConnectionString);
            conn.Open();
            using var cmd = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'", conn);
            using var reader = cmd.ExecuteReader();
    
            TableNames.Clear();
            while (reader.Read())
                TableNames.Add(reader.GetString(0));
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao carregar tabelas: " + ex.Message);
        }
    }

    public ObservableCollection<IEnumerable<object>> MyData { get; } = new() { new[] {"A", "B"}, new[] {"C", "D"} };
    public ObservableCollection<string> MyHeaders { get; } = new()  { "Field1", "Field2" };
    public ICommand NewWindowCommand { get; }

    private void ExecuteQuery()
    {
        try
        {
            using var conn = new SqlConnection(_connection.ConnectionString);
            conn.Open();

            var data  = conn.ExecuteReader(/*QueryText*/"", commandType: CommandType.Text);

            MyHeaders.Clear();
            
            for (int i = 0; i < data.FieldCount; i++)
            {
                MyHeaders.Add(data.GetName(i));
            }
            
            MyData.Clear();
            while (data.Read())
            {
                var values = new object[data.FieldCount];
                var count = data.GetValues(values);
                MyData.Add(values);
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao executar query: " + ex.Message);
        }
    }
}
