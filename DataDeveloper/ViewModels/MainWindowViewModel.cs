

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Dapper;
using Microsoft.Data.SqlClient;
using ReactiveUI;

namespace DataDeveloper.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string _queryText = "select top 100 * from Test1";
    public string QueryText
    {
        get => _queryText;
        set => this.RaiseAndSetIfChanged(ref _queryText, value);
    }

    // private DataView? _resultView;
    // public DataView? ResultView
    // {
    //     get =>  _resultView;
    //     set => this.RaiseAndSetIfChanged(ref _resultView, value);
    // }

    private ObservableCollection<RecordManager> _resultView = new();
    public ObservableCollection<RecordManager> ResultView
    {
        get => _resultView;
        set => this.RaiseAndSetIfChanged(ref _resultView, value);
    }
    public FlatTreeDataGridSource<RecordManager> Source { get; set; }

    // public ObservableCollection<ExpandoObject> QueryResults { get; } = new();
    // public ObservableCollection<string> Columns { get; } = new();    
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

        Source = new FlatTreeDataGridSource<RecordManager>(ResultView);

        ExecuteCommand = ReactiveCommand.Create(ExecuteQuery);
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
    
    private void ExecuteQuery()
    {
        try
        {
            using var conn = new SqlConnection(_connection.ConnectionString);
            conn.Open();

            // var result = conn.Query<ExpandoObject>(QueryText, commandType: CommandType.Text).ToList();
            //
            // if (result.Count > 0)
            // {
            //     var first = (IDictionary<string, object?>)result[0];
            //     foreach (var col in first.Keys)
            //         Columns.Add(col);
            // }
            //
            // foreach (var row in result)
            //     QueryResults.Add(row);
            
            var data  = conn.ExecuteReader(QueryText, commandType: CommandType.Text);

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
            
            // var columnList = new List<TextColumn<RecordManager, object>>();
            // for (int i = 0; i < data.FieldCount; i++)
            // {
            //     columnList.Add(new TextColumn<RecordManager, object>(data.GetName(i), r => r.GetValue()));
            // }
            //
            // ResultView.Clear();
            //
            // Source.Columns.Clear();
            // Source.Columns.AddRange(columnList);
            //
            // while (data.Read())
            // {
            //     var values = new object[data.FieldCount];
            //     var count = data.GetValues(values);
            //     ResultView.Add(new RecordManager(values));
            // }



            
            // using var cmd = new SqlCommand(QueryText, conn);
            // using var adapter = new SqlDataAdapter(cmd);
            //var table = new DataTable();
            //table.Load(data);
            // adapter.Fill(table);
            //ResultView = table.DefaultView;

        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao executar query: " + ex.Message);
        }
    }
}

public class RecordManager
{
    private int _index = -1;
    private readonly object[] _values;
    public RecordManager(object[] values)
    {
        _values = values;
    }
    public object GetValue()
    {
        _index++;
        return _values[_index];
    }
}