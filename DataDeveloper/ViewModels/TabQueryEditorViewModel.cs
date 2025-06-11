using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using DataDeveloper.Data;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Enums;
using DataDeveloper.EventAggregators;
using DataDeveloper.Events;
using DataDeveloper.Interfaces;
using DataDeveloper.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.ViewModels;

public class TabQueryEditorViewModel : BaseTabContent
{
    private string _queryText;
    private IEventAggregatorService _eventAggregatorService;
    public event EventHandler<int> ShowResultTool; 

    public TabQueryEditorViewModel(IConnectionSettings connectionSettings, string name, bool canClose, IServiceProvider serviceProvider) 
        : base(TabType.QueryEditor, name, canClose, serviceProvider)
    {
        _eventAggregatorService = this.ServiceProvider.GetService<IEventAggregatorService>();    
        
        ConnectionSettings = connectionSettings;
        
        ExecuteCommand = ReactiveCommand.CreateFromTask(ExecuteQuery, outputScheduler: RxApp.MainThreadScheduler);
        StopCommand = ReactiveCommand.CreateFromTask(StopQuery, outputScheduler: RxApp.MainThreadScheduler);
        CloseTabResultCommand = ReactiveCommand.Create<BaseTabContent>(CloseTabResult);

        Tabs.Add(new TabMessageViewModel("Message", false, filterId: this.Id, this.ServiceProvider));

        this.WhenAnyValue(vm => vm.CursorOffSet).Subscribe(_ => ShowCursorData());
        this.WhenAnyValue(vm => vm.CursorLine).Subscribe(_ => ShowCursorData());
        this.WhenAnyValue(vm => vm.CursorColumn).Subscribe(_ => ShowCursorData());
    }

    public void ShowCursorData()
    {
        _eventAggregatorService.Publish(new ShowCursorDataEvent(this.CursorOffSet, this.CursorLine, this.CursorColumn));
    }

    public IConnectionSettings ConnectionSettings { get; }
    private void CloseTabResult(BaseTabContent tabModel)
    {
        Tabs.Remove(tabModel);
    }

    [Reactive] public string SqlStatement { get; set; }
    [Reactive] public string SelectedStatement { get; set; }
    [Reactive] public int CursorOffSet { get; set; }
    [Reactive] public int CursorLine { get; set; }
    [Reactive] public int CursorColumn { get; set; }
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
            var statementExecutor = ConnectionSettings.GetStatementExecutor();

            var statementResults = await statementExecutor.ExecuteStatement(SelectedStatement.IsNullOrEmpty() ? SqlStatement : SelectedStatement);

            if (statementResults.Any())
            {
                for (var i = (Tabs.Count - 1); i > 0; i--)
                {
                    var tab = Tabs[i] as TabDataGridViewModel;
                    await tab.CloseDataReader();
                    Tabs.RemoveAt(i);
                }

                var index = 0;
                var resultMessage = new StringBuilder();
                foreach (var statementResult in statementResults)
                {
                    index++;
                    var hasRows = statementResult.DataReader.HasRows;

                    var resultName = $"result {index:00}";
                    if (hasRows)
                    {
                        var tabResult = new TabDataGridViewModel(statementResult, resultName, true, this.ServiceProvider); 
                        Tabs.Add(tabResult);
                        this.SelectedTabIndex = index;
                        await tabResult.LoadData();
                        resultMessage.AppendLine($"{tabResult.Rows.Count} record(s) returned for {resultName}\r\n");
                    }
                    else
                    {
                        resultMessage.AppendLine($"{statementResult.DataReader.RecordsAffected} record(s) affected for {resultName}\r\n");
                    }
                }
                _eventAggregatorService.Publish(new ShowResultMessageEvent(this.Id, resultMessage.ToString()));
            }
        }
        catch (Exception ex)
        {
            _eventAggregatorService.Publish(new ShowResultMessageEvent(this.Id, ex.Message));
        }
        finally
        {
            this.ResultIsMinimized = false;
            this.StatementIsRunning = false;
            this.ShowResultTool?.Invoke(this, this.SelectedTabIndex);
        }
    }
    
    protected override Task<bool> OnClose()
    {
        return base.OnClose();
    }
}

