using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using DataDeveloper.Data;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Services;
using DataDeveloper.Enums;
using DataDeveloper.EventAggregators;
using DataDeveloper.Interfaces;
using DataDeveloper.Models;
using DataDeveloper.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.ViewModels;

public class TabQueryEditorViewModel : BaseTabContent
{
    private static readonly HashSet<int> SupportedRunTimeouts = [15, 30, 60, 120, 240, 480];
    private readonly IEventAggregatorService _eventAggregatorService;
    private readonly Dictionary<string, int> _cachePages = new();
    private IStatementExecutor? _activeStatementExecutor;
    private CancellationTokenSource? _queryCancellationTokenSource;
    
    public event EventHandler<int>? ShowResultTool; 

    public TabQueryEditorViewModel(IConnectionSettings connectionSettings, string name, string? file, bool canClose, IServiceProvider serviceProvider) 
        : base(TabType.QueryEditor, name, canClose, serviceProvider)
    {
        _eventAggregatorService = this.ServiceProvider.GetRequiredService<IEventAggregatorService>();    
        
        ConnectionSettings = connectionSettings;
        File = file;
        SelectedRunTimeoutSeconds = NormalizeRunTimeout(connectionSettings.StatementTimeoutSeconds);

        ExecuteCommand = ReactiveCommand.CreateFromTask(ExecuteQuery, outputScheduler: RxApp.MainThreadScheduler);
        StopCommand = ReactiveCommand.CreateFromTask(StopQuery, outputScheduler: RxApp.MainThreadScheduler);
        CloseTabResultCommand = ReactiveCommand.CreateFromTask<BaseTabContent>(CloseTabResult);
        ShowResultCommand = ReactiveCommand.Create(() => { });

        Tabs.Add(new TabMessageViewModel("Message", false, filterId: this.Id, this.ServiceProvider));

        this.WhenAnyValue(vm => vm.StatementIsRunning).Subscribe(SetEditorOperationState);
        this.WhenAnyValue(vm => vm.CursorOffSet).Subscribe(_ => ShowCursorData());
        this.WhenAnyValue(vm => vm.CursorLine).Subscribe(_ => ShowCursorData());
        this.WhenAnyValue(vm => vm.CursorColumn).Subscribe(_ => ShowCursorData());
        this.WhenAnyValue(vm => vm.SqlStatement).Subscribe(_ =>
        {
            TextWasChanged = SqlStatement == null ? false : true;
            RefreshDetectedParameters();
        });
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
    [Reactive] public int SelectedStatementLength { get; set; }
    [Reactive] public int CursorOffSet { get; set; }
    [Reactive] public int CursorLine { get; set; }
    [Reactive] public int CursorColumn { get; set; }
    [Reactive] public double EditorHeadHeight { get; set; }
    [Reactive] public double ResultsHeaderHeight { get; set; }
    [Reactive] public bool TextWasChanged { get; set; }
    [Reactive] public bool StatementIsRunning { get; set; }
    [Reactive] public bool IsExecutionStatusVisible { get; set; }
    [Reactive] public string ExecutionStatusMessage { get; set; } = string.Empty;
    [Reactive] public bool ResultIsMinimized { get; set; } = true;
    [Reactive] public int SelectedTabIndex { get; set; }
    [Reactive] public int SelectedRunTimeoutSeconds { get; set; } = 60;
    public bool HasDetectedParameters => ParameterValues.Count > 0;
    
    public ObservableCollection<BaseTabContent> Tabs { get; } = new();
    public ObservableCollection<QueryParameterValue> ParameterValues { get; } = new();
    public ObservableCollection<int> RunTimeoutOptions { get; } = new() { 15, 30, 60, 120, 240, 480 };
    public ReactiveCommand<Unit, Unit> ExecuteCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<BaseTabContent, Unit> CloseTabResultCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowResultCommand { get; set; }
    
    private async Task StopQuery()
    {
        ExecutionStatusMessage = "Cancelling...";

        _queryCancellationTokenSource?.Cancel();
        _activeStatementExecutor?.Cancel();

        var activeLoads = Tabs
            .OfType<TabDataGridViewModel>()
            .Where(tab => tab.IsBusy)
            .ToList();

        foreach (var activeLoad in activeLoads)
            await activeLoad.CancelLoadingAsync();
    }

    private async Task ExecuteQuery()
    {
        using var queryCancellationTokenSource = new CancellationTokenSource();
        _queryCancellationTokenSource = queryCancellationTokenSource;
        this.StatementIsRunning = true;
        IsExecutionStatusVisible = true;
        ExecutionStatusMessage = "Executing query...";
        _eventAggregatorService.Publish(new ShowExecutionStatusEvent(true, "Executing query..."));
        await Task.Delay(100);

        try
        {
            var statementExecutor = ConnectionSettings.GetStatementExecutor();
            _activeStatementExecutor = statementExecutor;
            var previousResultTabs = Tabs.OfType<TabDataGridViewModel>().ToList();
            var cleanupWatcher = Stopwatch.StartNew();
            var statementToExecute = SelectedStatementLength > 0 ? SelectedStatement : SqlStatement;
            var parameterValues = BuildParameterValuesForExecution(statementToExecute);

            if (previousResultTabs.Count > 0)
            {
                IsExecutionStatusVisible = true;
                ExecutionStatusMessage = "Closing previous result set...";
                _eventAggregatorService.Publish(new ShowExecutionStatusEvent(true, "Closing previous result set..."));
                await Task.Delay(100);
                await Task.WhenAll(
                    previousResultTabs
                        .Where(tab => !tab.IsClosed)
                        .Select(tab => Task.Run(async () => await tab.CloseDataReader())));
            }

            cleanupWatcher.Stop();

            foreach (var previousTab in previousResultTabs)
                Tabs.Remove(previousTab);

            IsExecutionStatusVisible = true;
            ExecutionStatusMessage = "Executing query...";
            _eventAggregatorService.Publish(new ShowExecutionStatusEvent(true, "Executing query..."));

            var statementResults = (await statementExecutor.ExecuteStatement(
                statementToExecute,
                parameterValues,
                SelectedRunTimeoutSeconds,
                queryCancellationTokenSource.Token)).ToList();
            var shouldRefreshSchema = statementResults.Any(result => StatementExecutionClassifier.RequiresSchemaRefresh(result.Statement));

            if (statementResults.Any())
            {
                var index = 0;
                var statementCount = 0;
                var resultMessage = new StringBuilder();
                if (cleanupWatcher.Elapsed > TimeSpan.Zero && previousResultTabs.Count > 0)
                    resultMessage.AppendLine($"Previous result cleanup took {cleanupWatcher.Elapsed:c}\r\n");

                foreach (var statementResult in statementResults)
                {
                    statementResult.Watcher.Start();
                    statementCount++;

                    var hasDataReader = statementResult.HasResultSet;

                    var resultName = $"result {statementCount:00}";
                    
                    if (!_cachePages.ContainsKey(statementResult.Statement))
                        _cachePages[statementResult.Statement] = 100;
                    
                    if (hasDataReader)
                    {
                        index++;
                        IsExecutionStatusVisible = true;
                        ExecutionStatusMessage = "Loading first rows...";
                        _eventAggregatorService.Publish(new ShowExecutionStatusEvent(true, "Loading first rows..."));
                        await Task.Delay(100);
                        
                        var tabResult = new TabDataGridViewModel(
                            ConnectionSettings,
                            statementResult,
                            _cachePages[statementResult.Statement],
                            resultName,
                            true,
                            this.ServiceProvider,
                            this.Id,
                            SelectedRunTimeoutSeconds,
                            () =>
                            {
                                Dispatcher.UIThread.Post(() =>
                                {
                                    SelectedTabIndex = 0;
                                    ShowResultTool?.Invoke(this, SelectedTabIndex);
                                });
                            },
                            (isVisible, message) =>
                            {
                                StatementIsRunning = isVisible;
                                IsExecutionStatusVisible = isVisible;
                                ExecutionStatusMessage = message;
                            });
                        tabResult.IsEditorOperationInProgress = StatementIsRunning;
                        tabResult.WhenAnyValue(vm => vm.SelectedPage).Subscribe(page => _cachePages[statementResult.Statement] = page);
                        
                        Tabs.Add(tabResult);
                        this.SelectedTabIndex = index;
                        await tabResult.LoadData();
                        resultMessage.AppendLine($"{tabResult.GridRows.Count} record(s) returned for {resultName} in {statementResult.Watcher.Elapsed:c}\r\n");
                    }
                    else
                    {
                        resultMessage.AppendLine($"{statementResult.RecordsAffected} record(s) affected for {resultName} in {statementResult.Watcher.Elapsed:c}\r\n");
                        await statementResult.CloseDataReader();
                    }
                    statementResult.Watcher.Stop();
                }
                _eventAggregatorService.Publish(new ShowResultMessageEvent(this.Id, resultMessage.ToString()));
            }

            if (shouldRefreshSchema)
            {
                var ddlStatement = statementResults
                    .Select(result => result.Statement)
                    .FirstOrDefault(StatementExecutionClassifier.RequiresSchemaRefresh);

                _eventAggregatorService.Publish(new RefreshSchemaExplorerEvent(ConnectionSettings.Id, ddlStatement));
            }
        }
        catch (OperationCanceledException)
        {
            _eventAggregatorService.Publish(new ShowResultMessageEvent(this.Id, "Execution cancelled."));
            this.SelectedTabIndex = 0;
        }
        catch (Exception ex) when (IsTimeoutException(ex))
        {
            _eventAggregatorService.Publish(new ShowResultMessageEvent(
                this.Id,
                $"Query timed out after {SelectedRunTimeoutSeconds} seconds."));
            this.SelectedTabIndex = 0;
        }
        catch (Exception ex)
        {
            _eventAggregatorService.Publish(new ShowResultMessageEvent(this.Id, ex.Message));
            this.SelectedTabIndex = 0;
        }
        finally
        {
            if (ReferenceEquals(_queryCancellationTokenSource, queryCancellationTokenSource))
                _queryCancellationTokenSource = null;

            _activeStatementExecutor = null;
            this.ResultIsMinimized = false;
            this.StatementIsRunning = false;
            IsExecutionStatusVisible = false;
            ExecutionStatusMessage = string.Empty;
            _eventAggregatorService.Publish(new ShowExecutionStatusEvent(false, string.Empty));
            this.ShowResultTool?.Invoke(this, this.SelectedTabIndex);
        }
    }

    private void RefreshDetectedParameters()
    {
        var detectedParameters = SqlParameterDetector.ExtractParameters(SqlStatement);
        var existingValues = ParameterValues.ToDictionary(parameter => parameter.Name, parameter => parameter.Value, StringComparer.OrdinalIgnoreCase);

        ParameterValues.Clear();
        foreach (var parameter in detectedParameters)
        {
            existingValues.TryGetValue(parameter, out var existingValue);
            ParameterValues.Add(new QueryParameterValue(parameter, existingValue));
        }

        this.RaisePropertyChanged(nameof(HasDetectedParameters));
    }

    private IReadOnlyDictionary<string, object?>? BuildParameterValuesForExecution(string sql)
    {
        var parametersInStatement = SqlParameterDetector.ExtractParameters(sql);
        if (parametersInStatement.Count == 0)
            return null;

        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameterName in parametersInStatement)
        {
            var parameter = ParameterValues.FirstOrDefault(item => string.Equals(item.Name, parameterName, StringComparison.OrdinalIgnoreCase));
            values[parameterName] = SqlParameterValueConverter.Convert(parameter?.Value, parameter?.IsNull ?? true);
        }

        return values;
    }

    private void SetEditorOperationState(bool isRunning)
    {
        foreach (var tab in Tabs.OfType<TabDataGridViewModel>())
            tab.IsEditorOperationInProgress = isRunning;
    }

    private static bool IsTimeoutException(Exception exception)
    {
        foreach (var current in EnumerateExceptions(exception))
        {
            if (current is TimeoutException)
                return true;

            var message = current.Message;
            if (message.Contains("Execution Timeout Expired", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Command Timeout Expired", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("timeout period elapsed", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("query timed out", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("statement timeout", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("canceling statement due to statement timeout", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("lock wait timeout exceeded", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Exception> EnumerateExceptions(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
            yield return current;
    }

    private static int NormalizeRunTimeout(int timeoutSeconds)
    {
        return SupportedRunTimeouts.Contains(timeoutSeconds)
            ? timeoutSeconds
            : DataDeveloper.Data.Models.ConnectionSettings.DefaultStatementTimeoutSeconds;
    }
}
