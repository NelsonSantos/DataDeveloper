using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Reactive;
using Dapper;
using Dock.Model.ReactiveUI.Controls;
using Microsoft.Data.SqlClient;
using ReactiveUI;

namespace DataDeveloper.ViewModels;

public class EditorDocumentViewModel : Document
{
    private string _queryText = "select top 100 * from Test1";
    private bool _textWasChanged = false;
    private SqlConnectionInfo _connection;

    public event EventHandler RowClear; 
    public event EventHandler<object[]> RowAdded; 
    public event EventHandler ColunmsClear;
    public event EventHandler<string[]> ColunmsChanged;
    public string QueryText
    {
        get => _queryText;
        set
        {
            TextWasChanged = _queryText != value;
            this.RaiseAndSetIfChanged(ref _queryText, value);
        }
    }

    public bool TextWasChanged
    {
        get => _textWasChanged;
        set => this.RaiseAndSetIfChanged(ref _textWasChanged, value);
    }

    public override bool OnClose()
    {
        return !TextWasChanged;
    }

    public EditorDocumentViewModel()
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

        ExecuteCommand = ReactiveCommand.Create(ExecuteQuery);
    }

    public ReactiveCommand<Unit, Unit> ExecuteCommand { get; }
    private void ExecuteQuery()
    {
        try
        {
            using var conn = new SqlConnection(_connection.ConnectionString);
            conn.Open();

            var data  = conn.ExecuteReader(QueryText, commandType: CommandType.Text);

            this.ColunmsClear?.Invoke(this, EventArgs.Empty);
            var columns = new List<string>();
            for (int i = 0; i < data.FieldCount; i++)
            {
                columns.Add(data.GetName(i));
            }
            this.ColunmsChanged?.Invoke(this, columns.ToArray());
            
            this.RowClear?.Invoke(this, EventArgs.Empty);
            while (data.Read())
            {
                var values = new object[data.FieldCount];
                var count = data.GetValues(values);
                this.RowAdded?.Invoke(this, values);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao executar query: " + ex.Message);
        }
    }
    
}
