using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using DataDeveloper.Core;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models.SchemaCompare;
using DataDeveloper.Data.Services.SchemaCompare;
using DataDeveloper.Enums;
using DataDeveloper.Interfaces;
using DataDeveloper.Models.SchemaCompare;
using DataDeveloper.Services;
using DataDeveloper.Services.SchemaCompare;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.ViewModels;

public class SchemaCompareViewModel : ViewModelBase
{
    private readonly IConnectionDialogService _connectionDialogService;
    private readonly IDialogService _dialogService;

    public SchemaCompareViewModel(IConnectionDialogService connectionDialogService, IDialogService dialogService)
    {
        _connectionDialogService = connectionDialogService;
        _dialogService = dialogService;

        var canGoNext = this.WhenAnyValue(
            vm => vm.CurrentStep,
            vm => vm.SelectedDatabaseType,
            vm => vm.SelectedSourceConnection,
            vm => vm.SelectedDestinationConnection,
            vm => vm.IsBusyLoadingObjects,
            vm => vm.IsComparing,
            (_, _, _, _, _, _) => ComputeCanGoNext());

        var canGoBack = this.WhenAnyValue(
            vm => vm.CurrentStep,
            vm => vm.IsComparing,
            (step, isComparing) => step != SchemaCompareWizardStep.SelectDatabaseType && !isComparing);

        NextCommand = ReactiveCommand.CreateFromTask(GoNextAsync, canGoNext);
        BackCommand = ReactiveCommand.Create(GoBack, canGoBack);
        CancelCommand = ReactiveCommand.CreateFromTask<StyledElement>(CancelAsync);
        ChooseSourceConnectionCommand = ReactiveCommand.CreateFromTask<StyledElement>(ChooseSourceConnectionAsync);
        ChooseDestinationConnectionCommand = ReactiveCommand.CreateFromTask<StyledElement>(ChooseDestinationConnectionAsync);
        SelectAllObjectsCommand = ReactiveCommand.Create(() => SetChecklist(true));
        DeselectAllObjectsCommand = ReactiveCommand.Create(() => SetChecklist(false));
        SelectAllResultsCommand = ReactiveCommand.Create(() => SetResults(true));
        DeselectAllResultsCommand = ReactiveCommand.Create(() => SetResults(false));
        SaveScriptCommand = ReactiveCommand.CreateFromTask(SaveScriptAsync);
        ExecuteScriptCommand = ReactiveCommand.CreateFromTask<StyledElement>(ExecuteScriptAsync);
        CopyScriptCommand = ReactiveCommand.CreateFromTask<StyledElement>(CopyScriptToClipboardAsync);
        CancelComparisonCommand = ReactiveCommand.Create(RequestCancelComparison);

        this.WhenAnyValue(vm => vm.SelectedDatabaseType).Subscribe(_ => ResetFromDatabaseType());

        this.WhenAnyValue(vm => vm.CurrentStep).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(IsSelectDatabaseTypeStep));
            this.RaisePropertyChanged(nameof(IsSelectSourceStep));
            this.RaisePropertyChanged(nameof(IsSelectDestinationStep));
            this.RaisePropertyChanged(nameof(IsSelectObjectsStep));
            this.RaisePropertyChanged(nameof(IsReviewResultsStep));
            this.RaisePropertyChanged(nameof(IsFinalScriptStep));
            this.RaisePropertyChanged(nameof(NextButtonLabel));
            this.RaisePropertyChanged(nameof(IsSelectDatabaseTypeStepDone));
            this.RaisePropertyChanged(nameof(IsSelectSourceStepDone));
            this.RaisePropertyChanged(nameof(IsSelectDestinationStepDone));
            this.RaisePropertyChanged(nameof(IsSelectObjectsStepDone));
            this.RaisePropertyChanged(nameof(IsReviewResultsStepDone));
        });
    }

    public bool IsSelectDatabaseTypeStep => CurrentStep == SchemaCompareWizardStep.SelectDatabaseType;
    public bool IsSelectSourceStep => CurrentStep == SchemaCompareWizardStep.SelectSource;
    public bool IsSelectDestinationStep => CurrentStep == SchemaCompareWizardStep.SelectDestination;
    public bool IsSelectObjectsStep => CurrentStep == SchemaCompareWizardStep.SelectObjects;
    public bool IsReviewResultsStep => CurrentStep == SchemaCompareWizardStep.ReviewResults;
    public bool IsFinalScriptStep => CurrentStep == SchemaCompareWizardStep.FinalScript;

    // "Done" = the wizard has moved past this step; drives the stepper breadcrumb's checkmark/line state.
    public bool IsSelectDatabaseTypeStepDone => CurrentStep > SchemaCompareWizardStep.SelectDatabaseType;
    public bool IsSelectSourceStepDone => CurrentStep > SchemaCompareWizardStep.SelectSource;
    public bool IsSelectDestinationStepDone => CurrentStep > SchemaCompareWizardStep.SelectDestination;
    public bool IsSelectObjectsStepDone => CurrentStep > SchemaCompareWizardStep.SelectObjects;
    public bool IsReviewResultsStepDone => CurrentStep > SchemaCompareWizardStep.ReviewResults;

    public string NextButtonLabel => CurrentStep switch
    {
        SchemaCompareWizardStep.SelectObjects => "Compare",
        SchemaCompareWizardStep.ReviewResults => "Generate Script",
        _ => "Next"
    };

    public ObservableCollection<DatabaseType> AvailableDatabaseTypes { get; } = new(Enum.GetValues<DatabaseType>());
    public ObservableCollection<SchemaCompareObjectPickerItem> ObjectChecklist { get; } = new();
    public ObservableCollection<SchemaCompareResultRow> Results { get; } = new();

    [Reactive] public SchemaCompareWizardStep CurrentStep { get; private set; } = SchemaCompareWizardStep.SelectDatabaseType;
    [Reactive] public DatabaseType? SelectedDatabaseType { get; set; }
    [Reactive] public IConnectionSettings? SelectedSourceConnection { get; private set; }
    [Reactive] public IConnectionSettings? SelectedDestinationConnection { get; private set; }
    [Reactive] public bool IsBusyLoadingObjects { get; set; }
    [Reactive] public string? ErrorMessage { get; set; }
    [Reactive] public SchemaCompareResultRow? SelectedResultRow { get; set; }
    [Reactive] public string GeneratedScript { get; private set; } = string.Empty;
    [Reactive] public bool HasUnsavedOrUnexecutedScript { get; private set; }

    [Reactive] public bool IsComparing { get; private set; }
    [Reactive] public int ProgressCompleted { get; private set; }
    [Reactive] public int ProgressTotal { get; private set; }
    [Reactive] public string? ProgressCurrentObjectName { get; private set; }
    [Reactive] public string ProgressText { get; private set; } = string.Empty;
    [Reactive] public string CopyScriptButtonLabel { get; private set; } = "Copy to clipboard";

    public ReactiveCommand<Unit, Unit> NextCommand { get; }
    public ReactiveCommand<Unit, Unit> BackCommand { get; }
    public ReactiveCommand<StyledElement, Unit> CancelCommand { get; }
    public ReactiveCommand<StyledElement, Unit> ChooseSourceConnectionCommand { get; }
    public ReactiveCommand<StyledElement, Unit> ChooseDestinationConnectionCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectAllObjectsCommand { get; }
    public ReactiveCommand<Unit, Unit> DeselectAllObjectsCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectAllResultsCommand { get; }
    public ReactiveCommand<Unit, Unit> DeselectAllResultsCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveScriptCommand { get; }
    public ReactiveCommand<StyledElement, Unit> ExecuteScriptCommand { get; }
    public ReactiveCommand<StyledElement, Unit> CopyScriptCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelComparisonCommand { get; }

    private CancellationTokenSource? _comparisonCancellationTokenSource;

    private bool ComputeCanGoNext()
    {
        return CurrentStep switch
        {
            SchemaCompareWizardStep.SelectDatabaseType => SelectedDatabaseType is not null,
            SchemaCompareWizardStep.SelectSource => SelectedSourceConnection is not null,
            SchemaCompareWizardStep.SelectDestination => SelectedDestinationConnection is not null,
            SchemaCompareWizardStep.SelectObjects => !IsBusyLoadingObjects && !IsComparing,
            SchemaCompareWizardStep.ReviewResults => true,
            _ => false
        };
    }

    private void ResetFromDatabaseType()
    {
        SelectedSourceConnection = null;
        SelectedDestinationConnection = null;
        ObjectChecklist.Clear();
        Results.Clear();
        GeneratedScript = string.Empty;
        HasUnsavedOrUnexecutedScript = false;
    }

    private async Task GoNextAsync()
    {
        switch (CurrentStep)
        {
            case SchemaCompareWizardStep.SelectDatabaseType:
                CurrentStep = SchemaCompareWizardStep.SelectSource;
                break;

            case SchemaCompareWizardStep.SelectSource:
                CurrentStep = SchemaCompareWizardStep.SelectDestination;
                break;

            case SchemaCompareWizardStep.SelectDestination:
                await LoadObjectsAsync();
                CurrentStep = SchemaCompareWizardStep.SelectObjects;
                break;

            case SchemaCompareWizardStep.SelectObjects:
                await RunComparisonAsync();
                break;

            case SchemaCompareWizardStep.ReviewResults:
                GeneratedScript = SchemaCompareEngine.BuildFinalScript(
                    SelectedSourceConnection!.Name,
                    SelectedDestinationConnection!.Name,
                    SelectedDestinationConnection.DatabaseType,
                    Results.Where(row => row.IsChecked).Select(row => row.Result).ToList());
                HasUnsavedOrUnexecutedScript = true;
                CurrentStep = SchemaCompareWizardStep.FinalScript;
                break;
        }
    }

    private void GoBack()
    {
        CurrentStep = CurrentStep switch
        {
            SchemaCompareWizardStep.SelectSource => SchemaCompareWizardStep.SelectDatabaseType,
            SchemaCompareWizardStep.SelectDestination => SchemaCompareWizardStep.SelectSource,
            SchemaCompareWizardStep.SelectObjects => SchemaCompareWizardStep.SelectDestination,
            SchemaCompareWizardStep.ReviewResults => SchemaCompareWizardStep.SelectObjects,
            SchemaCompareWizardStep.FinalScript => SchemaCompareWizardStep.ReviewResults,
            _ => CurrentStep
        };
    }

    private async Task ChooseSourceConnectionAsync(StyledElement element)
    {
        if (SelectedDatabaseType is null)
            return;

        var window = element.GetParentWindow();
        var picked = await _connectionDialogService.ShowDialogAsync(window, SelectedDatabaseType);
        if (picked is null)
            return;

        SelectedSourceConnection = picked;
        ObjectChecklist.Clear();
        Results.Clear();
        GeneratedScript = string.Empty;
        HasUnsavedOrUnexecutedScript = false;
    }

    private async Task ChooseDestinationConnectionAsync(StyledElement element)
    {
        if (SelectedDatabaseType is null)
            return;

        var window = element.GetParentWindow();
        var picked = await _connectionDialogService.ShowDialogAsync(window, SelectedDatabaseType);
        if (picked is null)
            return;

        if (SelectedSourceConnection is not null && picked.Id == SelectedSourceConnection.Id)
        {
            await _dialogService.ShowMessageAsync("The destination must be a different connection than the source.", "Compare Schemas");
            return;
        }

        SelectedDestinationConnection = picked;
        Results.Clear();
        GeneratedScript = string.Empty;
        HasUnsavedOrUnexecutedScript = false;
    }

    private async Task LoadObjectsAsync()
    {
        ObjectChecklist.Clear();
        Results.Clear();
        GeneratedScript = string.Empty;
        HasUnsavedOrUnexecutedScript = false;

        if (SelectedSourceConnection is null)
            return;

        IsBusyLoadingObjects = true;
        ErrorMessage = null;
        try
        {
            var objects = await SchemaCompareObjectEnumerator.EnumerateAsync(SelectedSourceConnection);
            foreach (var objectRef in objects.OrderBy(o => o.ObjectType).ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase))
                ObjectChecklist.Add(new SchemaCompareObjectPickerItem(objectRef));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusyLoadingObjects = false;
        }
    }

    private async Task RunComparisonAsync()
    {
        var selectedObjects = ObjectChecklist.Where(item => item.IsChecked).Select(item => item.ObjectRef).ToList();
        if (selectedObjects.Count == 0 || SelectedSourceConnection is null || SelectedDestinationConnection is null)
            return;

        ErrorMessage = null;

        using var cancellationTokenSource = new CancellationTokenSource();
        _comparisonCancellationTokenSource = cancellationTokenSource;

        ProgressCompleted = 0;
        ProgressTotal = selectedObjects.Count;
        ProgressCurrentObjectName = null;
        ProgressText = $"0 of {selectedObjects.Count}";
        IsComparing = true;

        var progress = new Progress<(int Completed, int Total, string CurrentObjectName)>(p =>
        {
            ProgressCompleted = p.Completed;
            ProgressTotal = p.Total;
            ProgressCurrentObjectName = p.CurrentObjectName;
            ProgressText = $"{p.Completed} of {p.Total}";
        });

        var sourceConnection = SelectedSourceConnection;
        var destinationConnection = SelectedDestinationConnection;

        try
        {
            // Run on the thread pool, not the UI thread: even though CompareAsync awaits its DB
            // calls, the CPU-bound work between them (building diff scripts, walking columns, etc.)
            // is enough to keep the UI thread busy and the progress overlay feeling unresponsive.
            var comparisonResults = await Task.Run(() => SchemaCompareEngine.CompareAsync(
                sourceConnection, destinationConnection, selectedObjects, progress, cancellationTokenSource.Token));

            Results.Clear();
            foreach (var result in comparisonResults)
                Results.Add(new SchemaCompareResultRow(result));

            CurrentStep = SchemaCompareWizardStep.ReviewResults;
        }
        catch (OperationCanceledException)
        {
            // stay on the objects step
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsComparing = false;
            _comparisonCancellationTokenSource = null;
        }
    }

    private void RequestCancelComparison()
    {
        if (_comparisonCancellationTokenSource is { IsCancellationRequested: false } cts)
            cts.Cancel();
    }

    private void SetChecklist(bool isChecked)
    {
        foreach (var item in ObjectChecklist)
            item.IsChecked = isChecked;
    }

    private void SetResults(bool isChecked)
    {
        foreach (var row in Results.Where(r => r.CanToggle))
            row.IsChecked = isChecked;
    }

    private async Task SaveScriptAsync()
    {
        var suggestedName = $"schema-compare-{DateTime.UtcNow:yyyyMMdd-HHmmss}.sql";
        var filePath = await _dialogService.ShowSaveFileDialogAsync(suggestedName);
        if (string.IsNullOrEmpty(filePath))
            return;

        await File.WriteAllTextAsync(filePath, GeneratedScript);
        HasUnsavedOrUnexecutedScript = false;
    }

    private async Task CopyScriptToClipboardAsync(StyledElement element)
    {
        var clipboard = element.GetParentWindow().Clipboard;
        if (clipboard is null)
            return;

        await clipboard.SetTextAsync(GeneratedScript);

        CopyScriptButtonLabel = "Copied";
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        CopyScriptButtonLabel = "Copy to clipboard";
    }

    private async Task ExecuteScriptAsync(StyledElement element)
    {
        if (SelectedDestinationConnection is null)
            return;

        var confirm = await _dialogService.ShowDialogAsync(
            $"This will execute the generated script against \"{SelectedDestinationConnection.Name}\". Do you want to continue?",
            "Execute script", DialogButtons.YesNo, DialogIcon.Warning);
        if (confirm != DialogResult.Yes)
            return;

        var risk = await _dialogService.ShowDialogAsync(
            $"This script may create, alter, and drop objects in \"{SelectedDestinationConnection.Name}\". This action cannot be undone. Do you understand the risks and want to proceed?",
            "Execute script", DialogButtons.YesNo, DialogIcon.Warning);
        if (risk != DialogResult.Yes)
            return;

        try
        {
            await SchemaCompareEngine.ExecuteScriptAsync(SelectedDestinationConnection, GeneratedScript);
            HasUnsavedOrUnexecutedScript = false;
            await _dialogService.ShowMessageAsync("Script executed successfully.", "Execute script");
            element.GetParentWindow().Close();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync(ex.Message, "Execute script failed");
        }
    }

    private async Task CancelAsync(StyledElement element)
    {
        if (CurrentStep == SchemaCompareWizardStep.FinalScript && HasUnsavedOrUnexecutedScript)
        {
            var result = await _dialogService.ShowDialogAsync(
                "You haven't saved or executed this script. Save it before closing?",
                "Compare Schemas", DialogButtons.YesNo, DialogIcon.Warning);

            if (result == DialogResult.Yes)
            {
                await SaveScriptAsync();
                if (HasUnsavedOrUnexecutedScript)
                    return; // the save-file dialog was cancelled; stay open
            }
        }

        element.GetParentWindow().Close();
    }
}
