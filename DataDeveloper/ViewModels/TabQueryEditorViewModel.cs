using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using DataDeveloper.Data;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Enums;
using DataDeveloper.EventAggregators;
using DataDeveloper.Interfaces;
using DataDeveloper.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.ViewModels;

public class TabQueryEditorViewModel : BaseTabContent
{
    private readonly IEventAggregatorService _eventAggregatorService;
    private readonly Dictionary<string, int> _cachePages = new();
    
    public event EventHandler<int>? ShowResultTool; 

    public TabQueryEditorViewModel(IConnectionSettings connectionSettings, string name, string? file, bool canClose, IServiceProvider serviceProvider) 
        : base(TabType.QueryEditor, name, canClose, serviceProvider)
    {
        _eventAggregatorService = this.ServiceProvider.GetRequiredService<IEventAggregatorService>();    
        
        ConnectionSettings = connectionSettings;
        File = file;

        ExecuteCommand = ReactiveCommand.CreateFromTask(ExecuteQuery, outputScheduler: RxApp.MainThreadScheduler);
        StopCommand = ReactiveCommand.CreateFromTask(StopQuery, outputScheduler: RxApp.MainThreadScheduler);
        CloseTabResultCommand = ReactiveCommand.CreateFromTask<BaseTabContent>(CloseTabResult);
        ShowResultCommand = ReactiveCommand.Create(() => { });

        Tabs.Add(new TabMessageViewModel("Message", false, filterId: this.Id, this.ServiceProvider));

        this.WhenAnyValue(vm => vm.CursorOffSet).Subscribe(_ => ShowCursorData());
        this.WhenAnyValue(vm => vm.CursorLine).Subscribe(_ => ShowCursorData());
        this.WhenAnyValue(vm => vm.CursorColumn).Subscribe(_ => ShowCursorData());
        this.WhenAnyValue(vm => vm.SqlStatement).Subscribe(_ => TextWasChanged = SqlStatement == null ? false : true);
        this.WhenAnyValue(vm => vm.File).Subscribe(newFileName =>
        {
            if (System.IO.File.Exists(newFileName))
                Name = Path.GetFileName(newFileName);
        });

        if ((File?.IsNullOrEmpty() ?? true) == false)
            SqlStatement = System.IO.File.ReadAllText(File);
        TextWasChanged = false;
    }

    public void ShowCursorData()
    {
        _eventAggregatorService.Publish(new ShowCursorDataEvent(this.CursorOffSet, this.CursorLine, this.CursorColumn));
    }

    private async Task CloseTabResult(BaseTabContent tabModel)
    {
        if (tabModel is TabDataGridViewModel dataGridTab && !dataGridTab.IsClosed)
            await dataGridTab.CloseDataReader();

        Tabs.Remove(tabModel);
    }

    public IConnectionSettings ConnectionSettings { get; }
    [Reactive] public string? File { get; set; }
    [Reactive] public string SqlStatement { get; set; } = string.Empty;
    [Reactive] public string SelectedStatement { get; set; } = string.Empty;
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
                    if (Tabs[i] is not TabDataGridViewModel tab)
                        continue;

                    await tab.CloseDataReader();
                    Tabs.RemoveAt(i);
                }

                var index = 0;
                var statementCount = 0;
                var resultMessage = new StringBuilder();
                foreach (var statementResult in statementResults)
                {
                    statementResult.Watcher.Start();
                    statementCount++;
                    
                    var hasRows = statementResult.DataReader.HasRows;

                    var resultName = $"result {statementCount:00}";
                    
                    if (!_cachePages.ContainsKey(statementResult.Statement))
                        _cachePages[statementResult.Statement] = 100;
                    
                    if (hasRows)
                    {
                        index++;
                        
                        var tabResult = new TabDataGridViewModel(statementResult, _cachePages[statementResult.Statement], resultName, true, this.ServiceProvider);
                        tabResult.WhenAnyValue(vm => vm.SelectedPage).Subscribe(page => _cachePages[statementResult.Statement] = page);
                        
                        Tabs.Add(tabResult);
                        this.SelectedTabIndex = index;
                        await tabResult.LoadData();
                        resultMessage.AppendLine($"{tabResult.GridRows.Count} record(s) returned for {resultName} in {statementResult.Watcher.Elapsed:c}\r\n");
                    }
                    else
                    {
                        resultMessage.AppendLine($"{statementResult.DataReader.RecordsAffected} record(s) affected for {resultName} in {statementResult.Watcher.Elapsed:c}\r\n");
                        await statementResult.CloseDataReader();
                    }
                    statementResult.Watcher.Stop();
                }
                _eventAggregatorService.Publish(new ShowResultMessageEvent(this.Id, resultMessage.ToString()));
            }
        }
        catch (Exception ex)
        {
            _eventAggregatorService.Publish(new ShowResultMessageEvent(this.Id, ex.Message));
            this.SelectedTabIndex = 0;
        }
        finally
        {
            this.ResultIsMinimized = false;
            this.StatementIsRunning = false;
            this.ShowResultTool?.Invoke(this, this.SelectedTabIndex);
        }
    }
}
