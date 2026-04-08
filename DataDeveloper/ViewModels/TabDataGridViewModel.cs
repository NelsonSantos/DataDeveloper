using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using DataDeveloper.Data;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services;
using DataDeveloper.Enums;
using DataDeveloper.EventAggregators;
using DataDeveloper.Interfaces;
using DataDeveloper.Models;
using DynamicData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.ViewModels;

public class TabDataGridViewModel : BaseTabContent
{
    private readonly IEventAggregatorService _eventAggregatorService;
    private readonly Guid _messageTargetId;
    private readonly Action? _focusMessageTab;
    private readonly List<PendingDeletedGridRow> _pendingDeletedRows = [];

    public TabDataGridViewModel(
        IConnectionSettings connectionSettings,
        StatementResult statementResult,
        int selectedPage,
        string name,
        bool canClose,
        IServiceProvider serviceProvider,
        Guid messageTargetId,
        Action? focusMessageTab = null) 
        : base(TabType.DataGrid, name, canClose, serviceProvider)
    {
        _eventAggregatorService = ServiceProvider.GetRequiredService<IEventAggregatorService>();
        _messageTargetId = messageTargetId;
        _focusMessageTab = focusMessageTab;
        ConnectionSettings = connectionSettings;
        StatementResult = statementResult;

        SelectedPage = selectedPage;
        LoadNextPageCommand = ReactiveCommand.CreateFromTask(() => LoadNextPage(SelectedPage)
            , outputScheduler: RxApp.MainThreadScheduler
            , canExecute: this.WhenAnyValue(x => x.IsClosed).Select(_ => _ is false));
        LoadAllRecordsCommand = ReactiveCommand.CreateFromTask(() => LoadNextPage()
            , outputScheduler: RxApp.MainThreadScheduler
            , canExecute: this.WhenAnyValue(x => x.IsClosed).Select(_ => _ is false));
        DiscardChangesCommand = ReactiveCommand.Create(
            DiscardChanges,
            this.WhenAnyValue(vm => vm.HasPendingChanges, vm => vm.IsBusy, (hasPendingChanges, isBusy) => hasPendingChanges && !isBusy));
        AddRowCommand = ReactiveCommand.Create(
            AddRow,
            this.WhenAnyValue(vm => vm.IsEditableResult, vm => vm.IsBusy, (isEditableResult, isBusy) => isEditableResult && !isBusy));
        DeleteRowCommand = ReactiveCommand.Create(
            DeleteSelectedRow,
            this.WhenAnyValue(vm => vm.IsEditableResult, vm => vm.SelectedRowIndex, vm => vm.IsBusy,
                (isEditableResult, selectedRowIndex, isBusy) => isEditableResult && selectedRowIndex >= 0 && !isBusy));
        SubmitChangesCommand = ReactiveCommand.CreateFromTask(
            SubmitChangesAsync,
            this.WhenAnyValue(vm => vm.IsEditableResult, vm => vm.HasPendingChanges, vm => vm.HasValidationErrors, vm => vm.IsBusy,
                (isEditableResult, hasPendingChanges, hasValidationErrors, isBusy) =>
                    isEditableResult && hasPendingChanges && !hasValidationErrors && !isBusy));
        this.WhenAnyValue(vm => vm.IsEditableResult).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(EditabilityStatusText));
            this.RaisePropertyChanged(nameof(ShowReadOnlyReason));
        });
    }

    public async Task CloseDataReader()
    {
        if (IsClosed)
            return;

        await StatementResult.CloseDataReader();
        await Dispatcher.UIThread.InvokeAsync(() => this.IsClosed = true);
    }

    public async Task LoadData()
    {
        var dataReader = StatementResult.DataReader;
        if (dataReader is null)
            return;

        Headers.Clear();
        GridHeaders.Clear();
        GridColumnTypes.Clear();
        GridRows.Clear();
        _pendingDeletedRows.Clear();
        RowNumber = 0;
        var columnSchema = dataReader.GetColumnSchema();
        var columns = new List<ColumnHeader>();
        for (int i = 0; i < dataReader.FieldCount; i++)
        {
            var fieldType = dataReader.GetFieldType(i);
            var schema = i < columnSchema.Count ? columnSchema[i] : null;
            var columnHeader = new ColumnHeader()
            {
                Name = dataReader.GetName(i),
                Type = GetGridColumnType(fieldType, schema?.AllowDBNull == true),
                Alignment = GetFieldAlignment(fieldType)
            };
            columns.Add(columnHeader);
            GridHeaders.Add(columnHeader.Name);
            GridColumnTypes.Add(columnHeader.Type);
        }

        Headers.Add(columns);
        await ResolveEditabilityAsync(dataReader, columns.Select(column => column.Name).ToList());
        
        await LoadNextPage(SelectedPage);
        
    }

    private static Type GetGridColumnType(Type fieldType, bool allowDBNull)
    {
        if (!allowDBNull || !fieldType.IsValueType || Nullable.GetUnderlyingType(fieldType) is not null)
            return fieldType;

        return typeof(Nullable<>).MakeGenericType(fieldType);
    }

    private async Task LoadNextPage(int itemsPerPage = 0)
    {
        var dataReader = StatementResult.DataReader;
        if (dataReader is null)
            return;

        var countRecords = 0;
        StatementResult.Watcher.Start();
        this.IsBusy = true;
        await Task.Delay(100);
        var readedUntilEnd = true;
        while (dataReader.Read())
        {
            RowNumber++;
            countRecords++;
            
            var values = new object[dataReader.FieldCount];
            dataReader.GetValues(values);
            this.GridRows.Add(new EditableGridRow(values));
            
            if (itemsPerPage > 0 && countRecords == itemsPerPage)
            {
                readedUntilEnd = false;
                break;
            }
        }

        if (readedUntilEnd)
            await this.CloseDataReader();
        StatementResult.Watcher.Stop();
        this.TimeElapsed = StatementResult.Watcher.Elapsed;
        this.IsBusy = false;
        ValidateAllRows();
    }

    private async Task ResolveEditabilityAsync(DbDataReader dataReader, IReadOnlyCollection<string> resultColumns)
    {
        var schema = dataReader.GetColumnSchema();
        var tableNameHint = ResolveTableNameHint(schema);
        var primaryKeyColumnsHint = schema
            .Where(column => column.IsKey == true && !string.IsNullOrWhiteSpace(column.ColumnName))
            .Select(column => column.ColumnName!)
            .ToList();

        var metadata = await EditableResultSetMetadataResolver.ResolveAsync(
            ConnectionSettings,
            StatementResult.Statement,
            resultColumns,
            tableNameHint,
            primaryKeyColumnsHint);
        EditableMetadata = metadata;
        IsEditableResult = metadata.IsEditable;
        EditabilityReason = metadata.Reason ?? (metadata.IsEditable
            ? "Double-click or press F2/Enter on non-primary-key cells to edit."
            : "Result set is read-only.");
        ReadOnlyColumnIndexes.Clear();
        foreach (var columnIndex in metadata.PrimaryKeyColumns
                     .Select(pk => GridHeaders.IndexOf(pk.Name))
                     .Where(index => index >= 0))
        {
            ReadOnlyColumnIndexes.Add(columnIndex);
        }

        ValidateAllRows();
    }

    private static string? ResolveTableNameHint(ReadOnlyCollection<DbColumn> schema)
    {
        var tableColumns = schema
            .Where(column => !string.IsNullOrWhiteSpace(column.BaseTableName))
            .ToList();

        if (tableColumns.Count == 0)
            return null;

        var firstTable = tableColumns[0].BaseTableName;
        var firstSchema = tableColumns[0].BaseSchemaName;
        var sameTable = tableColumns.All(column =>
            string.Equals(column.BaseTableName, firstTable, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(column.BaseSchemaName, firstSchema, StringComparison.OrdinalIgnoreCase));

        if (!sameTable || string.IsNullOrWhiteSpace(firstTable))
            return null;

        return string.IsNullOrWhiteSpace(firstSchema)
            ? firstTable
            : $"{firstSchema}.{firstTable}";
    }

    private void DiscardChanges()
    {
        for (var index = GridRows.Count - 1; index >= 0; index--)
        {
            if (GridRows[index] is not EditableGridRow row)
                continue;

            if (row.State == EditableGridRowState.New)
            {
                GridRows.RemoveAt(index);
                continue;
            }

            row.UnmarkDeleted();
            row.RejectChanges();
            GridRows[index] = row;
        }

        foreach (var pendingDeletedRow in _pendingDeletedRows.OrderBy(item => item.OriginalIndex))
        {
            pendingDeletedRow.Row.UnmarkDeleted();
            var insertIndex = Math.Min(pendingDeletedRow.OriginalIndex, GridRows.Count);
            GridRows.Insert(insertIndex, pendingDeletedRow.Row);
        }

        _pendingDeletedRows.Clear();
        RowNumber = GridRows.Count;

        ValidateAllRows();
    }

    private void AddRow()
    {
        var row = new EditableGridRow(new object?[GridHeaders.Count], isNew: true);
        GridRows.Add(row);
        ValidateRow(row);
        RecalculatePendingChanges();
    }

    private void DeleteSelectedRow()
    {
        if (SelectedRowIndex < 0 || SelectedRowIndex >= GridRows.Count)
            return;

        if (GridRows[SelectedRowIndex] is not EditableGridRow row)
            return;

        if (row.State == EditableGridRowState.New)
        {
            GridRows.RemoveAt(SelectedRowIndex);
            SelectedRowIndex = Math.Min(SelectedRowIndex, GridRows.Count - 1);
            RowNumber = GridRows.Count;
            RecalculatePendingChanges();
            return;
        }

        row.MarkDeleted();
        _pendingDeletedRows.Add(new PendingDeletedGridRow(row, SelectedRowIndex));
        GridRows.RemoveAt(SelectedRowIndex);
        SelectedRowIndex = Math.Min(SelectedRowIndex, GridRows.Count - 1);
        RowNumber = GridRows.Count;
        RecalculatePendingChanges();
    }

    private Task SubmitChangesAsync()
    {
        return SubmitPendingChangesAsync();
    }

    private void RecalculatePendingChanges()
    {
        HasPendingChanges = _pendingDeletedRows.Count > 0 ||
                            GridRows.OfType<EditableGridRow>().Any(row => row.HasPendingChanges);
        this.RaisePropertyChanged(nameof(HasValidationErrors));
        this.RaisePropertyChanged(nameof(ValidationErrorCount));
        this.RaisePropertyChanged(nameof(ValidationSummary));
        this.RaisePropertyChanged(nameof(ShowValidationSummary));
        this.RaisePropertyChanged(nameof(PendingInsertCount));
        this.RaisePropertyChanged(nameof(PendingUpdateCount));
        this.RaisePropertyChanged(nameof(PendingDeleteCount));
        this.RaisePropertyChanged(nameof(PendingChangesSummary));
        this.RaisePropertyChanged(nameof(ShowPendingChangesSummary));
    }

    public void NotifyCellEdited(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= GridRows.Count)
            return;

        if (GridRows[rowIndex] is EditableGridRow row)
            ValidateRow(row);

        RecalculatePendingChanges();
    }

    private async Task SubmitPendingChangesAsync()
    {
        if (!IsEditableResult || EditableMetadata is null || string.IsNullOrWhiteSpace(EditableMetadata.TableName))
            return;

        IsBusy = true;
        var insertedCount = GridRows.OfType<EditableGridRow>().Count(row => row.State == EditableGridRowState.New);
        var updatedCount = GridRows.OfType<EditableGridRow>().Count(row => row.State == EditableGridRowState.Modified);
        var deletedCount = _pendingDeletedRows.Count;
        var provider = ConnectionSettings.GetDatabaseProvider();
        await using var connection = provider.GetConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            for (var index = 0; index < GridRows.Count; index++)
            {
                if (GridRows[index] is not EditableGridRow row || !row.HasPendingChanges)
                    continue;

                var command = row.State switch
                {
                    EditableGridRowState.New => EditableResultSetCommandBuilder.BuildInsert(
                        ConnectionSettings.DatabaseType,
                        EditableMetadata.TableName,
                        GridHeaders,
                        EditableMetadata.TableColumns,
                        row.ToArray()),
                    EditableGridRowState.Modified => EditableResultSetCommandBuilder.BuildUpdate(
                        ConnectionSettings.DatabaseType,
                        EditableMetadata.TableName,
                        GridHeaders,
                        EditableMetadata.TableColumns,
                        row.OriginalValues,
                        row.ToArray()),
                    EditableGridRowState.Deleted => EditableResultSetCommandBuilder.BuildDelete(
                        ConnectionSettings.DatabaseType,
                        EditableMetadata.TableName,
                        GridHeaders,
                        EditableMetadata.TableColumns,
                        row.OriginalValues),
                    _ => null
                };

                if (command is null)
                    continue;

                var affectedRows = await ExecuteCommandAsync(connection, transaction, command);
                EnsureExpectedRowsWereAffected(row.State, affectedRows, index);
            }

            foreach (var pendingDeletedRow in _pendingDeletedRows)
            {
                var command = EditableResultSetCommandBuilder.BuildDelete(
                    ConnectionSettings.DatabaseType,
                    EditableMetadata.TableName,
                    GridHeaders,
                    EditableMetadata.TableColumns,
                    pendingDeletedRow.Row.OriginalValues);

                if (command is null)
                    continue;

                var affectedRows = await ExecuteCommandAsync(connection, transaction, command);
                EnsureExpectedRowsWereAffected(EditableGridRowState.Deleted, affectedRows, pendingDeletedRow.OriginalIndex);
            }

            await transaction.CommitAsync();
            _pendingDeletedRows.Clear();

            for (var index = GridRows.Count - 1; index >= 0; index--)
            {
                if (GridRows[index] is not EditableGridRow row)
                    continue;

                if (row.HasPendingChanges)
                {
                    row.AcceptChanges();
                    GridRows[index] = row;
                }
            }

            RowNumber = GridRows.Count;
            RecalculatePendingChanges();
            await RefreshDataAsync();
            PublishSubmitMessage(insertedCount, updatedCount, deletedCount);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            PublishSubmitFailureMessage(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshDataAsync()
    {
        await StatementResult.CloseDataReader();
        var statementExecutor = ConnectionSettings.GetStatementExecutor();
        var refreshedResult = (await statementExecutor.ExecuteStatement(StatementResult.Statement)).FirstOrDefault(result => result.HasResultSet);
        if (refreshedResult is null)
            return;

        StatementResult = refreshedResult;
        IsClosed = false;
        await LoadData();
    }

    private void PublishSubmitMessage(int insertedCount, int updatedCount, int deletedCount)
    {
        var message = $"Submit completed for {Name}:{Environment.NewLine}" +
                      $"Inserted: {insertedCount}{Environment.NewLine}" +
                      $"Updated: {updatedCount}{Environment.NewLine}" +
                      $"Deleted: {deletedCount}{Environment.NewLine}" +
                      $"Refreshed using:{Environment.NewLine}{StatementResult.Statement}";
        _eventAggregatorService.Publish(new ShowResultMessageEvent(_messageTargetId, message));
        _focusMessageTab?.Invoke();
    }

    private void PublishSubmitFailureMessage(Exception exception)
    {
        var message = $"Submit failed for {Name}:{Environment.NewLine}{exception.Message}";
        _eventAggregatorService.Publish(new ShowResultMessageEvent(_messageTargetId, message));
        _focusMessageTab?.Invoke();
    }

    private static void EnsureExpectedRowsWereAffected(EditableGridRowState rowState, int affectedRows, int rowIndex)
    {
        if (rowState is not (EditableGridRowState.Modified or EditableGridRowState.Deleted))
            return;

        if (affectedRows > 0)
            return;

        var operation = rowState == EditableGridRowState.Modified ? "update" : "delete";
        throw new InvalidOperationException(
            $"Could not {operation} row {rowIndex + 1}. The record may have been changed or removed by another process.");
    }

    private static async Task<int> ExecuteCommandAsync(DbConnection connection, DbTransaction transaction, EditableResultSetCommand command)
    {
        await using var dbCommand = connection.CreateCommand();
        dbCommand.Transaction = transaction;
        dbCommand.CommandType = CommandType.Text;
        dbCommand.CommandText = command.Sql;

        foreach (var parameter in command.Parameters)
        {
            var dbParameter = dbCommand.CreateParameter();
            dbParameter.ParameterName = NormalizeParameterName(parameter.Key, connection);
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            dbCommand.Parameters.Add(dbParameter);
        }

        return await dbCommand.ExecuteNonQueryAsync();
    }

    private static string NormalizeParameterName(string parameterName, DbConnection connection)
    {
        var trimmedName = parameterName.TrimStart('@', ':');
        return connection.GetType().Namespace?.Contains("Oracle", StringComparison.OrdinalIgnoreCase) == true
            ? trimmedName
            : $"@{trimmedName}";
    }

    public string EditabilityStatusText => IsEditableResult ? "Editable" : "Read-only";

    public bool ShowReadOnlyReason => !IsEditableResult;

    public int PendingInsertCount => GridRows.OfType<EditableGridRow>().Count(row => row.State == EditableGridRowState.New);

    public int PendingUpdateCount => GridRows.OfType<EditableGridRow>().Count(row => row.State == EditableGridRowState.Modified);

    public int PendingDeleteCount => _pendingDeletedRows.Count;

    public bool ShowPendingChangesSummary => HasPendingChanges;

    public bool HasValidationErrors => GridRows.OfType<EditableGridRow>().Any(row => row.HasValidationErrors);

    public int ValidationErrorCount => GridRows.OfType<EditableGridRow>().Sum(row => row.ValidationErrorCount);

    public bool ShowValidationSummary => HasValidationErrors;

    public string ValidationSummary => ValidationErrorCount == 1
        ? "1 required field pending"
        : $"{ValidationErrorCount} required fields pending";

    public string PendingChangesSummary
    {
        get
        {
            var parts = new List<string>();
            if (PendingInsertCount > 0)
                parts.Add($"{PendingInsertCount} inserted");
            if (PendingUpdateCount > 0)
                parts.Add($"{PendingUpdateCount} updated");
            if (PendingDeleteCount > 0)
                parts.Add($"{PendingDeleteCount} deleted");

            return string.Join(", ", parts);
        }
    }

    private void ValidateAllRows()
    {
        foreach (var row in GridRows.OfType<EditableGridRow>())
            ValidateRow(row);

        RecalculatePendingChanges();
    }

    private void ValidateRow(EditableGridRow row)
    {
        row.ClearValidationErrors();

        if (EditableMetadata is null)
            return;

        for (var columnIndex = 0; columnIndex < GridHeaders.Count && columnIndex < row.Count; columnIndex++)
        {
            var column = EditableMetadata.TableColumns.FirstOrDefault(item =>
                string.Equals(item.Name, GridHeaders[columnIndex], StringComparison.OrdinalIgnoreCase));
            if (column is null)
                continue;

            var requiresExplicitValue = !column.IsNullable &&
                                        !column.IsAutoGenerated &&
                                        !column.HasDefaultValue;

            if (requiresExplicitValue && IsMissingRequiredValue(row[columnIndex]))
                row.SetValidationError(columnIndex, $"'{column.Name}' is required.");
        }
    }

    private static bool IsMissingRequiredValue(object? value)
    {
        return value switch
        {
            null => true,
            DBNull => true,
            string text => string.IsNullOrWhiteSpace(text),
            _ => false
        };
    }

    public ColumnAlignment GetFieldAlignment(Type fieldType)
    {
        return fieldType.FullName?.ToLowerInvariant() switch
        {
            "system.boolean" or
            "system.byte" or
            "system.sbyte" => ColumnAlignment.Center,

            "system.int16" or 
            "system.int32" or 
            "system.int64" or 
            "system.uint16" or 
            "system.uint32" or
            "system.double" or
            "system.decimal" => ColumnAlignment.Far,
            
            "system.string" or "system.char" or "system.guid" => ColumnAlignment.Near,
            
            "system.datetime" or
            "system.datetimeoffset" or
            "system.timespan" => ColumnAlignment.Center,
            
            "system.byte[]" or "system.dbnull" or "system.object" or "system.xml.xmldocument" or _ => ColumnAlignment.Near,
        };
    }

    public IConnectionSettings ConnectionSettings { get; }
    public StatementResult StatementResult { get; private set; }
    [Reactive] public EditableResultSetMetadata? EditableMetadata { get; private set; }
    [Reactive] public bool IsEditableResult { get; private set; }
    [Reactive] public string? EditabilityReason { get; private set; }
    [Reactive] public bool HasPendingChanges { get; private set; }
    [Reactive] public int SelectedRowIndex { get; set; } = -1;
    [Reactive] public TimeSpan TimeElapsed { get; set; }
    [Reactive] public bool IsClosed { get; set; }
    [Reactive] public int RowNumber { get; set; }
    [Reactive] public int SelectedPage { get; set; }
    public ObservableCollection<ColumnHeader> Headers { get; } = new();
    public ObservableCollection<string> GridHeaders { get; } = new();
    public ObservableCollection<Type> GridColumnTypes { get; } = new();
    public ObservableCollection<IReadOnlyList<object?>> GridRows { get; } = new();
    public ObservableCollection<int> ReadOnlyColumnIndexes { get; } = new();
    public ObservableCollection<int> Pages { get; } = new() { 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000, 100000, };
    public ReactiveCommand<Unit, Unit> LoadNextPageCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadAllRecordsCommand { get; }
    public ReactiveCommand<Unit, Unit> DiscardChangesCommand { get; }
    public ReactiveCommand<Unit, Unit> AddRowCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteRowCommand { get; }
    public ReactiveCommand<Unit, Unit> SubmitChangesCommand { get; }
}
