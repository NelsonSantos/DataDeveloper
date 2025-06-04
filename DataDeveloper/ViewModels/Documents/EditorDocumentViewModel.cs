using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Dapper;
using DataDeveloper.Data;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Enums;
using DataDeveloper.Events;
using DataDeveloper.Models;
using Dock.Model.ReactiveUI.Controls;
using Microsoft.Data.SqlClient;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.ViewModels;

public class EditorDocumentViewModel : Document
{
    private readonly IConnectionSettings _connectionSettings;
    private readonly IStatementExecutor _statementExecutor;
    private string _queryText;

    public event EventHandler<ShowMessageEventArgs> ShowMessage;
    public event EventHandler<int> ShowResultTool; 
    
    public EditorDocumentViewModel(IConnectionSettings connectionSettings)
    {
        _connectionSettings = connectionSettings;
        _statementExecutor = _connectionSettings.GetStatementExecutor();
        
        ExecuteCommand = ReactiveCommand.CreateFromTask(ExecuteQuery, outputScheduler: RxApp.MainThreadScheduler);
        StopCommand = ReactiveCommand.CreateFromTask(StopQuery, outputScheduler: RxApp.MainThreadScheduler);
        CloseTabResultCommand = ReactiveCommand.Create<TabResult>(CloseTabResult);

        Tabs.Add(new TabResultMessage("Message", false));
    }

    private void CloseTabResult(TabResult tabModel)
    {
        Tabs.Remove(tabModel);
    }

    public string QueryText
    {
        get => _queryText;
        set
        {
            if (_queryText != null)
                TextWasChanged = _queryText != value;
            this.RaiseAndSetIfChanged(ref _queryText, value);
        }
    }
    [Reactive] public double EditorHeadHeight { get; set; }
    [Reactive] public double ResultsHeaderHeight { get; set; }
    [Reactive] public bool TextWasChanged { get; set; }
    [Reactive] public bool StatementIsRunning { get; set; }
    [Reactive] public bool ResultIsMinimized { get; set; } = true;
    [Reactive] public int SelectedTabIndex { get; set; }
    
    public ObservableCollection<TabResult> Tabs { get; } = new();
    public ReactiveCommand<Unit, Unit> ExecuteCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<TabResult, Unit> CloseTabResultCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowResultCommand { get; set; }
    public override bool OnClose()
    {
        return !TextWasChanged;
    }
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
            var statementExecutor = _connectionSettings.GetStatementExecutor();

            var statementResults = await statementExecutor.ExecuteStatement(QueryText);

            if (statementResults.Any())
            {
                for (var i = (Tabs.Count - 1); i > 0; i--)
                {
                    var tab = Tabs[i] as TabResultDataGrid;
                    await tab.CloseDataReader();
                    Tabs.RemoveAt(i);
                }

                var index = 0;
                foreach (var statementResult in statementResults)
                {
                    index++;
                    var resultName = $"result {index:00}";
                    var tabResult = new TabResultDataGrid(statementResult, resultName, true); 
                    Tabs.Add(tabResult);
                    this.SelectedTabIndex = index;
                    await tabResult.LoadData();
                }

                this.ResultIsMinimized = false;
                this.ShowResultTool?.Invoke(this, this.SelectedTabIndex); ///here, for now, I'll send 0 (zero) for result tab 0
            }

            // TODO trocar isso por um component
            //var connectionSettingsSql = _connectionSettings as SqlServerConnectionSettings;

            //var connectionString = $"Server={connectionSettingsSql.Server};Database={connectionSettingsSql.Database};User Id={connectionSettingsSql.User};Password={connectionSettingsSql.Password};TrustServerCertificate=True;";

            /*
            await using var conn = new SqlConnection(connectionString);
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

            this.SelectedTabIndex = 1;
            this.ResultIsMinimized = false;
            this.ShowResultTool?.Invoke(this, this.SelectedTabIndex); ///here, for now, I'll send 0 (zero) for result tab 0

                                                                      */
        }
        catch (Exception ex)
        {
            //this.ShowMessage?.Invoke(this, new ShowMessageEventArgs(ex.Message, true));
        }
        finally
        {
            this.StatementIsRunning = false;
        }
    }
}