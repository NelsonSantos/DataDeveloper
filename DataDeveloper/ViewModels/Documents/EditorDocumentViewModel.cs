using System;
using System.Collections.Generic;
using System.Data;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using Dapper;
using DataDeveloper.Events;
using DataDeveloper.Models;
using Dock.Model.ReactiveUI.Controls;
using Microsoft.Data.SqlClient;
using ReactiveUI;

namespace DataDeveloper.ViewModels;

public class EditorDocumentViewModel : Document
{
    private string _queryText = "select top 100 * from Test1";
    private bool _textWasChanged = false;
    private SqlConnectionInfo _connection;
    private bool _statementIsRunning;

    public event EventHandler RowClear; 
    public event EventHandler<RowValues> RowAdded; 
    public event EventHandler ColumnsClear;
    public event EventHandler<string[]> ColumnsChanged;
    public event EventHandler<ShowMessageEventArgs> ShowMessage;
    public event EventHandler<int> ShowResultTool; 
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

    public bool StatementIsRunning
    {
        get => _statementIsRunning;
        set => this.RaiseAndSetIfChanged(ref _statementIsRunning, value);
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

        ExecuteCommand = ReactiveCommand.CreateFromTask(ExecuteQuery, outputScheduler: RxApp.MainThreadScheduler);
        StopCommand = ReactiveCommand.CreateFromTask(StopQuery, outputScheduler: RxApp.MainThreadScheduler);
    }

    public ReactiveCommand<Unit, Unit> ExecuteCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }

    private async Task StopQuery()
    {
        await Task.Delay(100);
        try
        {

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
        finally
        {
            this.StatementIsRunning = false;
        }
    }

    private async Task ExecuteQuery()
    {
        this.StatementIsRunning = true;
        await Task.Delay(100);

        try
        {
            await using var conn = new SqlConnection(_connection.ConnectionString);
            conn.Open();

            var data = await conn.ExecuteReaderAsync(QueryText, commandType: CommandType.Text);

            this.ColumnsClear?.Invoke(this, EventArgs.Empty);
            var columns = new List<string>();
            for (int i = 0; i < data.FieldCount; i++)
            {
                columns.Add(data.GetName(i));
            }

            this.ColumnsChanged?.Invoke(this, columns.ToArray());

            this.RowClear?.Invoke(this, EventArgs.Empty);
            var rowNumber = 0;
            while (data.Read())
            {
                rowNumber++;
                var values = new object?[data.FieldCount];
                data.GetValues(values);
                this.RowAdded?.Invoke(this, new RowValues(rowNumber, values));
            }

            this.ShowResultTool?.Invoke(this, 0); ///here, for now, I'll send 0 (zero) for result tab 0
            
        }
        catch (Exception ex)
        {
            this.ShowMessage?.Invoke(this, new ShowMessageEventArgs(ex.Message, true));
        }
        finally
        {
            this.StatementIsRunning = false;
        }
    }
   

}