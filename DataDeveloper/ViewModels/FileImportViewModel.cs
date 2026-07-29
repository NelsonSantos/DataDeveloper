using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using DataDeveloper.Core;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Models.FileImport;
using DataDeveloper.Data.Models.TableDesigner;
using DataDeveloper.Data.Services.FileImport;
using DataDeveloper.Data.Services.TableDesigner;
using DataDeveloper.Enums;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.ViewModels;

public class FileImportViewModel : ViewModelBase
{
    private const int PreviewSampleRowCount = 50;

    private readonly IConnectionDialogService _connectionDialogService;
    private readonly IDialogService _dialogService;
    private ISchemaExplorer? _schemaExplorer;
    private List<ColumnModel> _existingTableColumns = new();

    public FileImportViewModel(IConnectionDialogService connectionDialogService, IDialogService dialogService, IConnectionSettings? preselectedConnection = null)
    {
        _connectionDialogService = connectionDialogService;
        _dialogService = dialogService;

        if (preselectedConnection is not null)
        {
            SelectedConnection = preselectedConnection;
            CurrentStep = FileImportWizardStep.SelectFile;
        }

        var canGoNext = this.WhenAnyValue(
            vm => vm.CurrentStep,
            vm => vm.SelectedConnection,
            vm => vm.FilePreview,
            vm => vm.TargetMode,
            vm => vm.NewTableName,
            vm => vm.SelectedExistingTable,
            vm => vm.IsBusy,
            vm => vm.IsImporting,
            (_, _, _, _, _, _, _, _) => ComputeCanGoNext());

        var canGoBack = this.WhenAnyValue(
            vm => vm.CurrentStep,
            vm => vm.IsImporting,
            (step, isImporting) => step != FileImportWizardStep.SelectConnection && !isImporting);

        NextCommand = ReactiveCommand.CreateFromTask(GoNextAsync, canGoNext);
        BackCommand = ReactiveCommand.Create(GoBack, canGoBack);
        CloseCommand = ReactiveCommand.Create<StyledElement>(element => element.GetParentWindow().Close(), this.WhenAnyValue(vm => vm.IsImporting, isImporting => !isImporting));
        ChooseConnectionCommand = ReactiveCommand.CreateFromTask<StyledElement>(ChooseConnectionAsync);
        ChooseFileCommand = ReactiveCommand.CreateFromTask(ChooseFileAsync);
        SelectNewTableModeCommand = ReactiveCommand.Create(() => TargetMode = FileImportTargetMode.NewTable);
        SelectExistingTableModeCommand = ReactiveCommand.Create(() => TargetMode = FileImportTargetMode.ExistingTable);

        this.WhenAnyValue(vm => vm.CurrentStep).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(IsSelectConnectionStep));
            this.RaisePropertyChanged(nameof(IsSelectFileStep));
            this.RaisePropertyChanged(nameof(IsChooseTargetStep));
            this.RaisePropertyChanged(nameof(IsMapColumnsStep));
            this.RaisePropertyChanged(nameof(IsReviewStep));
            this.RaisePropertyChanged(nameof(IsResultStep));
            this.RaisePropertyChanged(nameof(IsSelectConnectionStepDone));
            this.RaisePropertyChanged(nameof(IsSelectFileStepDone));
            this.RaisePropertyChanged(nameof(IsChooseTargetStepDone));
            this.RaisePropertyChanged(nameof(IsMapColumnsStepDone));
            this.RaisePropertyChanged(nameof(IsReviewStepDone));
            this.RaisePropertyChanged(nameof(NextButtonLabel));
        });

        this.WhenAnyValue(vm => vm.TargetMode).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(IsNewTableMode));
            this.RaisePropertyChanged(nameof(IsExistingTableMode));
        });

        this.WhenAnyValue(vm => vm.ImportResult).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(HasImportErrors));
            this.RaisePropertyChanged(nameof(IsImportFailed));
            this.RaisePropertyChanged(nameof(IsImportPartiallySuccessful));
            this.RaisePropertyChanged(nameof(IsImportFullySuccessful));
        });
    }

    public bool IsSelectConnectionStep => CurrentStep == FileImportWizardStep.SelectConnection;
    public bool IsSelectFileStep => CurrentStep == FileImportWizardStep.SelectFile;
    public bool IsChooseTargetStep => CurrentStep == FileImportWizardStep.ChooseTarget;
    public bool IsMapColumnsStep => CurrentStep == FileImportWizardStep.MapColumns;
    public bool IsReviewStep => CurrentStep == FileImportWizardStep.Review;
    public bool IsResultStep => CurrentStep == FileImportWizardStep.Result;

    // "Done" = the wizard has moved past this step; drives the stepper breadcrumb's checkmark/line state.
    public bool IsSelectConnectionStepDone => CurrentStep > FileImportWizardStep.SelectConnection;
    public bool IsSelectFileStepDone => CurrentStep > FileImportWizardStep.SelectFile;
    public bool IsChooseTargetStepDone => CurrentStep > FileImportWizardStep.ChooseTarget;
    public bool IsMapColumnsStepDone => CurrentStep > FileImportWizardStep.MapColumns;
    public bool IsReviewStepDone => CurrentStep > FileImportWizardStep.Review;

    public bool IsNewTableMode => TargetMode == FileImportTargetMode.NewTable;
    public bool IsExistingTableMode => TargetMode == FileImportTargetMode.ExistingTable;

    public string NextButtonLabel => CurrentStep switch
    {
        FileImportWizardStep.Review => "Import",
        _ => "Next"
    };

    public ObservableCollection<FileImportTableOption> AvailableTables { get; } = new();
    public ObservableCollection<FileImportColumnMappingRowViewModel> MappingRows { get; } = new();

    [Reactive] public FileImportWizardStep CurrentStep { get; private set; } = FileImportWizardStep.SelectConnection;
    [Reactive] public IConnectionSettings? SelectedConnection { get; private set; }
    [Reactive] public string? FilePath { get; private set; }
    [Reactive] public FileImportPreview? FilePreview { get; private set; }
    [Reactive] public FileImportTargetMode TargetMode { get; set; } = FileImportTargetMode.NewTable;
    [Reactive] public string NewTableName { get; set; } = string.Empty;
    [Reactive] public string NewSchemaName { get; set; } = string.Empty;
    [Reactive] public FileImportTableOption? SelectedExistingTable { get; set; }
    [Reactive] public bool IsBusy { get; set; }
    [Reactive] public bool IsImporting { get; private set; }
    [Reactive] public string? ErrorMessage { get; set; }
    [Reactive] public string GeneratedCreateScript { get; private set; } = string.Empty;
    [Reactive] public int RowCountToImport { get; private set; }
    [Reactive] public int ProgressCompleted { get; private set; }
    [Reactive] public int ProgressTotal { get; private set; }
    [Reactive] public string ProgressText { get; private set; } = string.Empty;
    [Reactive] public FileImportResult? ImportResult { get; private set; }
    public bool HasImportErrors => ImportResult?.RowsFailed > 0;

    /// <summary>No rows imported at all — shown with the red/failure icon on the result step.</summary>
    public bool IsImportFailed => ImportResult is not null && ImportResult.RowsImported == 0;

    /// <summary>Some rows imported, some failed — shown with the yellow/warning icon.</summary>
    public bool IsImportPartiallySuccessful => ImportResult is { RowsImported: > 0, RowsFailed: > 0 };

    /// <summary>Every row imported with no failures — shown with the green/success icon.</summary>
    public bool IsImportFullySuccessful => ImportResult is { RowsImported: > 0, RowsFailed: 0 };

    public ReactiveCommand<Unit, Unit> NextCommand { get; }
    public ReactiveCommand<Unit, Unit> BackCommand { get; }
    public ReactiveCommand<StyledElement, Unit> CloseCommand { get; }
    public ReactiveCommand<StyledElement, Unit> ChooseConnectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ChooseFileCommand { get; }
    public ReactiveCommand<Unit, FileImportTargetMode> SelectNewTableModeCommand { get; }
    public ReactiveCommand<Unit, FileImportTargetMode> SelectExistingTableModeCommand { get; }

    private bool ComputeCanGoNext()
    {
        return CurrentStep switch
        {
            FileImportWizardStep.SelectConnection => SelectedConnection is not null,
            FileImportWizardStep.SelectFile => FilePreview is not null && !IsBusy,
            FileImportWizardStep.ChooseTarget => !IsBusy && (TargetMode == FileImportTargetMode.NewTable
                ? !string.IsNullOrWhiteSpace(NewTableName)
                : SelectedExistingTable is not null),
            FileImportWizardStep.MapColumns => MappingRows.Any(row => row.IsIncluded),
            FileImportWizardStep.Review => !IsImporting,
            _ => false
        };
    }

    private async Task GoNextAsync()
    {
        switch (CurrentStep)
        {
            case FileImportWizardStep.SelectConnection:
                CurrentStep = FileImportWizardStep.SelectFile;
                break;

            case FileImportWizardStep.SelectFile:
                await LoadAvailableTablesAsync();
                CurrentStep = FileImportWizardStep.ChooseTarget;
                break;

            case FileImportWizardStep.ChooseTarget:
                await PrepareColumnMappingAsync();
                CurrentStep = FileImportWizardStep.MapColumns;
                break;

            case FileImportWizardStep.MapColumns:
                PrepareReview();
                CurrentStep = FileImportWizardStep.Review;
                break;

            case FileImportWizardStep.Review:
                await RunImportAsync();
                CurrentStep = FileImportWizardStep.Result;
                break;
        }
    }

    private void GoBack()
    {
        CurrentStep = CurrentStep switch
        {
            FileImportWizardStep.SelectFile => FileImportWizardStep.SelectConnection,
            FileImportWizardStep.ChooseTarget => FileImportWizardStep.SelectFile,
            FileImportWizardStep.MapColumns => FileImportWizardStep.ChooseTarget,
            FileImportWizardStep.Review => FileImportWizardStep.MapColumns,
            FileImportWizardStep.Result => FileImportWizardStep.Review,
            _ => CurrentStep
        };
    }

    private async Task ChooseConnectionAsync(StyledElement element)
    {
        var window = element.GetParentWindow();
        var picked = await _connectionDialogService.ShowDialogAsync(window);
        if (picked is null)
            return;

        if (SelectedConnection is null || picked.Id != SelectedConnection.Id)
        {
            _schemaExplorer = null;
            AvailableTables.Clear();
            SelectedExistingTable = null;
        }

        SelectedConnection = picked;
    }

    private async Task ChooseFileAsync()
    {
        var filePath = await _dialogService.ShowOpenImportFileAsync("Import file...");
        if (string.IsNullOrEmpty(filePath))
            return;

        ErrorMessage = null;
        IsBusy = true;
        try
        {
            FilePreview = await Task.Run(() => FileImportReader.ReadPreview(filePath, PreviewSampleRowCount));
            FilePath = filePath;

            if (string.IsNullOrWhiteSpace(NewTableName))
                NewTableName = FileImportTableNameSanitizer.Sanitize(Path.GetFileNameWithoutExtension(filePath));
        }
        catch (Exception ex)
        {
            FilePreview = null;
            FilePath = null;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadAvailableTablesAsync()
    {
        if (SelectedConnection is null)
            return;

        AvailableTables.Clear();
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            _schemaExplorer ??= SelectedConnection.GetSchemaExplorer();
            await _schemaExplorer.InitializeSchemaNode();

            var tablesFolder = _schemaExplorer.RootConnections.FirstOrDefault()?.Children
                .FirstOrDefault(child => child.NodeType == NodeType.Tables);

            if (tablesFolder is null)
                return;

            foreach (var tableNode in tablesFolder.Children.Where(child => child.NodeType == NodeType.Table))
            {
                var (schemaName, tableName) = SplitObjectName(tableNode.Name);
                AvailableTables.Add(new FileImportTableOption(tableNode.Name, schemaName, tableName, tableNode));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PrepareColumnMappingAsync()
    {
        MappingRows.Clear();
        ErrorMessage = null;

        if (FilePreview is null || SelectedConnection is null)
            return;

        if (TargetMode == FileImportTargetMode.ExistingTable && SelectedExistingTable is not null)
            _existingTableColumns = await LoadExistingTableColumnsAsync(SelectedExistingTable.Node);

        var headers = FilePreview.Headers;
        for (var index = 0; index < headers.Count; index++)
        {
            var row = new FileImportColumnMappingRowViewModel(index, headers[index]);

            if (TargetMode == FileImportTargetMode.NewTable)
            {
                var sampleValues = FilePreview.SampleRows.Select(sampleRow => index < sampleRow.Count ? sampleRow[index] : null);
                var suggested = FileImportTypeInferrer.SuggestColumn(SelectedConnection.DatabaseType, headers[index], sampleValues);
                var availableDataTypes = new ObservableCollection<ProviderDataTypeOption>(ProviderDataTypeCatalog.GetDataTypes(SelectedConnection.DatabaseType));
                var selectedType = availableDataTypes.FirstOrDefault(type => string.Equals(type.Name, suggested.DataType, StringComparison.OrdinalIgnoreCase))
                    ?? availableDataTypes.FirstOrDefault();

                row.NewColumn = new TableDesignerColumnViewModel(availableDataTypes, selectedType!)
                {
                    Name = suggested.Name,
                    Length = suggested.Length,
                    Precision = suggested.Precision,
                    Scale = suggested.Scale,
                    IsNullable = suggested.IsNullable
                };
            }
            else
            {
                row.AvailableTargetColumns.Add(string.Empty);
                foreach (var column in _existingTableColumns)
                    row.AvailableTargetColumns.Add(column.Name);

                var matchedColumn = _existingTableColumns.FirstOrDefault(column => string.Equals(column.Name, headers[index], StringComparison.OrdinalIgnoreCase));
                row.TargetColumnName = matchedColumn?.Name ?? string.Empty;
                row.IsIncluded = matchedColumn is not null;
            }

            MappingRows.Add(row);
        }
    }

    private async Task<List<ColumnModel>> LoadExistingTableColumnsAsync(SchemaNode tableNode)
    {
        var columnsFolder = tableNode.Children.FirstOrDefault(child => child.NodeType == NodeType.Columns);
        if (columnsFolder is null || _schemaExplorer is null)
            return [];

        if (columnsFolder.CanLoad)
            await _schemaExplorer.LoadNodeAsync(columnsFolder);

        return columnsFolder.Children
            .Where(child => child.NodeType == NodeType.Column && child.Tag is ColumnModel)
            .Select(child => (ColumnModel)child.Tag!)
            .ToList();
    }

    private void PrepareReview()
    {
        if (FilePath is null)
            return;

        RowCountToImport = FileImportReader.ReadAllRows(FilePath).Count();
        GeneratedCreateScript = TargetMode == FileImportTargetMode.NewTable && SelectedConnection is not null
            ? TableDdlScriptBuilder.BuildCreateTableScript(SelectedConnection.DatabaseType, BuildNewTableDefinition())
            : string.Empty;
    }

    private async Task RunImportAsync()
    {
        if (SelectedConnection is null || FilePath is null)
            return;

        IsImporting = true;
        ErrorMessage = null;
        ProgressCompleted = 0;
        ProgressTotal = RowCountToImport;
        ProgressText = $"0 of {RowCountToImport}";

        var progress = new Progress<(int Completed, int Total)>(reported =>
        {
            ProgressCompleted = reported.Completed;
            ProgressTotal = reported.Total;
            ProgressText = $"{reported.Completed} of {reported.Total}";
        });

        try
        {
            var mappings = BuildColumnMappings();
            string tableName;
            IReadOnlyList<ColumnModel> tableColumns;

            if (TargetMode == FileImportTargetMode.NewTable)
            {
                var tableDefinition = BuildNewTableDefinition();
                await FileImportEngine.CreateTableAsync(SelectedConnection, tableDefinition);
                tableColumns = FileImportEngine.BuildColumnModels(tableDefinition);
                tableName = string.IsNullOrWhiteSpace(tableDefinition.SchemaName)
                    ? tableDefinition.TableName
                    : $"{tableDefinition.SchemaName}.{tableDefinition.TableName}";
            }
            else
            {
                tableColumns = _existingTableColumns;
                tableName = SelectedExistingTable!.DisplayName;
            }

            ImportResult = await FileImportEngine.ImportRowsAsync(SelectedConnection, tableName, tableColumns, FilePath, mappings, progress);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsImporting = false;
        }
    }

    private TableDefinition BuildNewTableDefinition()
    {
        var tableDefinition = new TableDefinition
        {
            SchemaName = NewSchemaName,
            TableName = NewTableName
        };

        tableDefinition.Columns.AddRange(MappingRows
            .Where(row => row.IsIncluded && row.NewColumn is not null)
            .Select(row => new TableColumnDefinition
            {
                Name = row.NewColumn!.Name,
                DataType = row.NewColumn.SelectedDataType?.Name ?? string.Empty,
                Length = row.NewColumn.Length,
                Precision = row.NewColumn.Precision,
                Scale = row.NewColumn.Scale,
                IsNullable = row.NewColumn.IsNullable
            }));

        return tableDefinition;
    }

    private List<FileImportColumnMapping> BuildColumnMappings()
    {
        return MappingRows.Select(row =>
        {
            var mapping = new FileImportColumnMapping(row.SourceColumnIndex, row.SourceColumnName)
            {
                IsIncluded = row.IsIncluded
            };

            if (TargetMode == FileImportTargetMode.NewTable && row.NewColumn is not null)
            {
                mapping.NewColumn = new TableColumnDefinition
                {
                    Name = row.NewColumn.Name,
                    DataType = row.NewColumn.SelectedDataType?.Name ?? string.Empty,
                    Length = row.NewColumn.Length,
                    Precision = row.NewColumn.Precision,
                    Scale = row.NewColumn.Scale,
                    IsNullable = row.NewColumn.IsNullable
                };
            }
            else if (TargetMode == FileImportTargetMode.ExistingTable)
            {
                mapping.TargetColumnName = string.IsNullOrWhiteSpace(row.TargetColumnName) ? null : row.TargetColumnName;
                if (mapping.TargetColumnName is null)
                    mapping.IsIncluded = false;
            }

            return mapping;
        }).ToList();
    }

    private static (string SchemaName, string TableName) SplitObjectName(string objectName)
    {
        var parts = objectName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 1
            ? (string.Join(".", parts.Take(parts.Length - 1)), parts[^1])
            : (string.Empty, objectName);
    }
}

public sealed class FileImportTableOption
{
    public FileImportTableOption(string displayName, string schemaName, string tableName, SchemaNode node)
    {
        DisplayName = displayName;
        SchemaName = schemaName;
        TableName = tableName;
        Node = node;
    }

    public string DisplayName { get; }
    public string SchemaName { get; }
    public string TableName { get; }
    public SchemaNode Node { get; }

    public override string ToString() => DisplayName;
}

public sealed class FileImportColumnMappingRowViewModel : ViewModelBase
{
    public FileImportColumnMappingRowViewModel(int sourceColumnIndex, string sourceColumnName)
    {
        SourceColumnIndex = sourceColumnIndex;
        SourceColumnName = sourceColumnName;
    }

    public int SourceColumnIndex { get; }
    public string SourceColumnName { get; }

    [Reactive] public bool IsIncluded { get; set; } = true;
    [Reactive] public TableDesignerColumnViewModel? NewColumn { get; set; }
    [Reactive] public string? TargetColumnName { get; set; }

    public ObservableCollection<string> AvailableTargetColumns { get; } = new();
}
