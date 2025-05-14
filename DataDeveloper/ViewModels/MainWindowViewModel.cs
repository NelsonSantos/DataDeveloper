

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Reactive;
using Dapper;
using Microsoft.Data.SqlClient;
using ReactiveUI;

namespace DataDeveloper.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string _queryText = "SELECT * FROM sys.tables";
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

    // private ObservableCollection<ExpandoObject>? _resultView;
    // public ObservableCollection<ExpandoObject>? ResultView
    // {
    //     get => _resultView;
    //     set => this.RaiseAndSetIfChanged(ref _resultView, value);
    // }

    public ObservableCollection<ExpandoObject> QueryResults { get; } = new();
    public ObservableCollection<string> Columns { get; } = new();    
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
            Database = "NXGenMarketplace_Payment_Development",
            Password = "123mudar",
            Server = "192.168.239.48",
            User = "user_app",
        };
        LoadTables();
    
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
    
    private void ExecuteQuery()
    {
        try
        {
            using var conn = new SqlConnection(_connection.ConnectionString);
            conn.Open();

            var result = conn.Query<ExpandoObject>(QueryText, commandType: CommandType.Text).ToList();

            if (result.Count > 0)
            {
                var first = (IDictionary<string, object?>)result[0];
                foreach (var col in first.Keys)
                    Columns.Add(col);
            }

            foreach (var row in result)
                QueryResults.Add(row);
            
            // var data  = conn.Query<ExpandoObject>(QueryText, commandType: CommandType.Text);
            //
            // ResultView = new ObservableCollection<ExpandoObject>(data);

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