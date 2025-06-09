using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using DataDeveloper.Data;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Enums;
using DataDeveloper.Events;
using DataDeveloper.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.ViewModels;

public class TabQueryEditorViewModel : BaseTabContent
{
    private readonly IConnectionSettings _connectionSettings;
    private readonly IStatementExecutor _statementExecutor;
    private string _queryText;

    public event EventHandler<ShowMessageEventArgs> ShowMessage;
    public event EventHandler<int> ShowResultTool; 
    
    public TabQueryEditorViewModel(IConnectionSettings connectionSettings, string name, bool canClose) 
        : base(TabType.QueryEditor, name, canClose)
    {
        ConnectionSettings = connectionSettings;
        _statementExecutor = _connectionSettings.GetStatementExecutor();
        
        ExecuteCommand = ReactiveCommand.CreateFromTask(ExecuteQuery, outputScheduler: RxApp.MainThreadScheduler);
        StopCommand = ReactiveCommand.CreateFromTask(StopQuery, outputScheduler: RxApp.MainThreadScheduler);
        CloseTabResultCommand = ReactiveCommand.Create<BaseTabContent>(CloseTabResult);

        Tabs.Add(new TabMessageViewModel("Message", false));
    }
    public IConnectionSettings ConnectionSettings { get; }
    private void CloseTabResult(BaseTabContent tabModel)
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
    
    public ObservableCollection<BaseTabContent> Tabs { get; } = new();
    public ReactiveCommand<Unit, Unit> ExecuteCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<BaseTabContent, Unit> CloseTabResultCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowResultCommand { get; set; }
    
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
                    var tab = Tabs[i] as TabDataGridViewModel;
                    await tab.CloseDataReader();
                    Tabs.RemoveAt(i);
                }

                var index = 0;
                foreach (var statementResult in statementResults)
                {
                    index++;
                    var resultName = $"result {index:00}";
                    var tabResult = new TabDataGridViewModel(statementResult, resultName, true); 
                    Tabs.Add(tabResult);
                    this.SelectedTabIndex = index;
                    await tabResult.LoadData();
                }

                this.ResultIsMinimized = false;
                this.ShowResultTool?.Invoke(this, this.SelectedTabIndex); ///here, for now, I'll send 0 (zero) for result tab 0
            }
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
    
    protected override Task<bool> OnClose()
    {
        return base.OnClose();
    }
}

