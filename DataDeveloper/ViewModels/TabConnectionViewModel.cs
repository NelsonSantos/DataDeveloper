using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Avalonia;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Models.TableDesigner;
using DataDeveloper.Data.Services;
using DataDeveloper.Data.Services.TableDesigner;
using DataDeveloper.Enums;
using DataDeveloper.EventAggregators;
using DataDeveloper.Interfaces;
using DataDeveloper.Models;
using DataDeveloper.Services;
using DataDeveloper.Views;
using DynamicData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.ViewModels;

public class TabConnectionViewModel : BaseTabContent
{
    private int _countQueryEditors = 0;
    private readonly IDialogService _dialogService;
    private readonly IFileImportDialogService _fileImportDialogService;
    private readonly IEventAggregatorService _eventAggregatorService;
    private readonly IProviderSqlAnalyzer _sqlAnalyzer;
    private readonly ISessionTabStore _sessionTabStore;
    private readonly Subject<Unit> _sessionChangeTrigger = new();
    private readonly Dictionary<Guid, IDisposable> _editorAutosaveSubscriptions = new();
    private bool _autosaveSuspended;

    public TabConnectionViewModel(IConnectionSettings connectionSettings, bool canClose, IServiceProvider serviceProvider)
        : base(TabType.Connection, connectionSettings.Name, canClose, serviceProvider)
    {
        ConnectionSettings = connectionSettings;
        SchemaExplorer = ConnectionSettings.GetSchemaExplorer();
        _sqlAnalyzer = ConnectionSettings.GetSqlAnalyzer();
        _dialogService = ServiceProvider.GetRequiredService<IDialogService>();
        _fileImportDialogService = ServiceProvider.GetRequiredService<IFileImportDialogService>();
        _eventAggregatorService = ServiceProvider.GetRequiredService<IEventAggregatorService>();
        _sessionTabStore = ServiceProvider.GetRequiredService<ISessionTabStore>();

        this.Initialization = LoadConnection();

        CloseTabQueryEditorCommand = ReactiveCommand.CreateFromTask<TabQueryEditorViewModel, bool>(tab => CloseTabQueryEditor(tab));
        AddQueryEditorCommand = ReactiveCommand.Create<string?>(AddQueryEditor);
        CreateTableCommand = ReactiveCommand.CreateFromTask<StyledElement>(CreateTableAsync);
        ImportFileCommand = ReactiveCommand.CreateFromTask<StyledElement>(ImportFileAsync);
        RefreshCommand = ReactiveCommand.CreateFromTask(Refresh);
        RestoreSessionOrAddDefaultEditor();
        SetupAutosave();
        this.WhenAnyValue(vm => vm.SelectedEditor).Subscribe(_ =>
        {
            if (this.SelectedEditor < 0) return;
            
            QueryEditors[this.SelectedEditor].ShowCursorData();
        });

        _eventAggregatorService.Subscribe<RefreshSchemaExplorerEvent>(
            this,
            async message =>
            {
                if (!string.IsNullOrWhiteSpace(message.Statement))
                    await SchemaExplorer.RefreshSchemaObjectAsync(message.Statement);
                else
                    await SchemaExplorer.RefreshSchemaAsync();
            },
            message => message.ConnectionId == ConnectionSettings.Id);
    }

    private async Task Refresh()
    {
        await LoadConnection();
    }

    public async Task CreateTableAsync(StyledElement source)
    {
        var viewModel = new TableDesignerViewModel(
            ConnectionSettings,
            async script => await ExecuteBackgroundStatementWithResultAsync(script, refreshSchema: true),
            _dialogService,
            SchemaExplorer);

        var window = new TableDesignerWindow(viewModel)
        {
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
        };

        await window.ShowDialog(source.GetParentWindow());
    }

    public async Task ImportFileAsync(StyledElement source)
    {
        await _fileImportDialogService.ShowDialogAsync(source.GetParentWindow(), ConnectionSettings);
        await SchemaExplorer.RefreshSchemaAsync();
    }

    public async Task EditTableAsync(SchemaNode node, StyledElement source)
    {
        var columnsFolder = node.Children.FirstOrDefault(child => child.NodeType == NodeType.Columns);
        if (columnsFolder is not null && columnsFolder.CanLoad)
            await SchemaExplorer.LoadNodeAsync(columnsFolder);

        var loadedColumns = (columnsFolder?.Children ?? Enumerable.Empty<SchemaNode>())
            .Where(child => child.NodeType == NodeType.Column && child.Tag is ColumnModel)
            .Select(child => (ColumnModel)child.Tag!)
            .ToList();

        var (schemaName, tableName) = SplitTableObjectName(node.Name);

        TableDefinition originalDefinition;
        try
        {
            originalDefinition = await TableDefinitionLoader.LoadAsync(ConnectionSettings, schemaName, tableName, loadedColumns);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync(ex.Message, "Edit table");
            return;
        }

        var viewModel = TableDesignerViewModel.CreateForEdit(
            ConnectionSettings,
            originalDefinition,
            async script => await ExecuteBackgroundStatementTransactionallyAsync(script, refreshSchema: true),
            _dialogService,
            SchemaExplorer);

        var window = new TableDesignerWindow(viewModel)
        {
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
        };

        await window.ShowDialog(source.GetParentWindow());
    }

    private static (string SchemaName, string TableName) SplitTableObjectName(string objectName)
    {
        var parts = objectName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 1
            ? (string.Join(".", parts.Take(parts.Length - 1)), parts[^1])
            : (string.Empty, objectName);
    }

    public async Task ExecuteBackgroundStatementAsync(string statement, bool refreshSchema = false)
    {
        await ExecuteBackgroundStatementWithResultAsync(statement, refreshSchema);
    }

    private async Task<bool> ExecuteBackgroundStatementWithResultAsync(string statement, bool refreshSchema = false)
    {
        try
        {
            var statements = _sqlAnalyzer.SplitStatements(statement);
            if (statements.Count == 0)
                return true;

            await using var connection = ConnectionSettings.GetDatabaseProvider().GetConnection();
            await connection.OpenAsync();

            foreach (var executableStatement in statements)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = executableStatement;
                await command.ExecuteNonQueryAsync();
            }

            if (refreshSchema || _sqlAnalyzer.RequiresSchemaRefresh(statement))
                await SchemaExplorer.RefreshSchemaObjectAsync(statement);

            return true;
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync(ex.Message, "Execution error");
            return false;
        }
    }

    /// <summary>
    /// Applies a multi-statement script inside an explicit transaction, used only for the
    /// edit-table apply path (ALTER scripts can include a drop, so a partial failure should
    /// not leave the earlier statements committed). The create-table apply path
    /// (<see cref="ExecuteBackgroundStatementWithResultAsync"/>) is left untouched to avoid
    /// regressing it. Note MySQL and Oracle DDL statements auto-commit regardless of this
    /// wrapper — that is a provider limitation, not a bug here.
    /// </summary>
    private async Task<bool> ExecuteBackgroundStatementTransactionallyAsync(string statement, bool refreshSchema = false)
    {
        try
        {
            var statements = _sqlAnalyzer.SplitStatements(statement);
            if (statements.Count == 0)
                return true;

            await using var connection = ConnectionSettings.GetDatabaseProvider().GetConnection();
            await connection.OpenAsync();
            var transaction = await connection.BeginTransactionAsync();
            await using (transaction.ConfigureAwait(false))
            {
                try
                {
                    foreach (var executableStatement in statements)
                    {
                        await using var command = connection.CreateCommand();
                        command.CommandText = executableStatement;
                        command.Transaction = transaction;
                        await command.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            if (refreshSchema || _sqlAnalyzer.RequiresSchemaRefresh(statement))
                await SchemaExplorer.RefreshSchemaObjectAsync(statement);

            return true;
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync(ex.Message, "Execution error");
            return false;
        }
    }

    public async Task<bool> ConfirmDropAsync(SchemaNode node)
    {
        var objectType = node.NodeType switch
        {
            NodeType.Table => "table",
            NodeType.View => "view",
            NodeType.Procedure => "procedure",
            NodeType.Function => "function",
            _ => null
        };

        if (objectType is null)
            return false;

        var message = $"Are you sure you want to drop the {objectType} {node.Name}?";
        var result = await _dialogService.ShowDialogAsync(
            message,
            "Confirm drop",
            DialogButtons.YesNo,
            DialogIcon.Warning);

        return result == DialogResult.Yes;
    }

    public async Task<bool> SaveChanges(TabQueryEditorViewModel tabQueryEditor, bool isSaveAs = false)
    {
        string? filePath;
        if (isSaveAs || tabQueryEditor.File.IsNullOrEmpty())
        {
            var fileName = $"{tabQueryEditor.Name}{(tabQueryEditor.Name.EndsWith("sql", StringComparison.OrdinalIgnoreCase) ? "" : ".sql")}";
            filePath = await  _dialogService.ShowSaveFileDialogAsync(fileName);
            if (filePath.IsNullOrEmpty())
            {
                return false;
            }
        }
        else
        {
            filePath = tabQueryEditor.File;
        }
        await File.WriteAllTextAsync(filePath!, tabQueryEditor.SqlStatement);
        tabQueryEditor.File = filePath;
        tabQueryEditor.TextWasChanged = false;
        return true;
    }

    public async Task<bool> CloseTabQueryEditor(TabQueryEditorViewModel tabQueryEditor, bool showDialog = true, bool persistSession = false)
    {
        var remove = true;

        if (tabQueryEditor.HasActiveTransaction)
        {
            SelectedEditor = QueryEditors.IndexOf(tabQueryEditor);
            await Task.Delay(100);

            var result = showDialog
                ? await _dialogService.ShowDialogAsync(
                    $"{tabQueryEditor.Name} has a pending transaction.\n\r\r\nYes commits the changes, No rolls them back, and Cancel keeps the tab open.",
                    "Pending transaction",
                    DialogButtons.YesNoCancel,
                    DialogIcon.Warning)
                : DialogResult.No;

            switch (result)
            {
                case DialogResult.Yes:
                    remove = await tabQueryEditor.CommitPendingTransaction();
                    break;
                case DialogResult.No:
                    remove = await tabQueryEditor.RollbackPendingTransaction();
                    break;
                case DialogResult.Cancel:
                    remove = false;
                    break;
            }
        }

        if (remove && tabQueryEditor.TextWasChanged && !persistSession)
        {
            SelectedEditor = QueryEditors.IndexOf(tabQueryEditor);
            await Task.Delay(100);

            var result = showDialog
                ? await _dialogService.ShowDialogResult($"{tabQueryEditor.Name} was changed...\n\r\r\nDo you want to save that changes?")
                : DialogResult.Yes;

            switch (result)
            {
                case DialogResult.Yes:
                    await Task.Delay(100);
                    remove = await SaveChanges(tabQueryEditor);
                    break;
                case DialogResult.No:
                    remove = true;
                    break;
                case DialogResult.Cancel:
                    remove = false;
                    break;
            }
        }

        if (remove)
            QueryEditors.Remove(tabQueryEditor);

        return remove;
    }

    public void PersistSessionSnapshot()
    {
        var editors = QueryEditors
            .Where(HasMeaningfulContent)
            .Select(editor => new EditorTabState
            {
                Name = editor.Name,
                File = editor.File,
                SqlStatement = editor.SqlStatement,
                IsDirty = editor.TextWasChanged
            })
            .ToList();

        if (editors.Count == 0)
        {
            _sessionTabStore.Remove(ConnectionSettings.Id);
            return;
        }

        _sessionTabStore.Save(ConnectionSettings.Id, editors);
    }

    private static bool HasMeaningfulContent(TabQueryEditorViewModel editor)
    {
        return !string.IsNullOrWhiteSpace(editor.SqlStatement) || !string.IsNullOrEmpty(editor.File);
    }

    public void SuspendAutosave() => _autosaveSuspended = true;
    public void ResumeAutosave() => _autosaveSuspended = false;

    private void SetupAutosave()
    {
        _sessionChangeTrigger
            .Throttle(TimeSpan.FromSeconds(1.5))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                if (!_autosaveSuspended)
                    PersistSessionSnapshot();
            });

        foreach (var editor in QueryEditors)
            TrackEditorForAutosave(editor);

        QueryEditors.CollectionChanged += OnQueryEditorsChanged;
    }

    private void OnQueryEditorsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (TabQueryEditorViewModel editor in e.NewItems)
                TrackEditorForAutosave(editor);

        if (e.OldItems is not null)
            foreach (TabQueryEditorViewModel editor in e.OldItems)
                UntrackEditorForAutosave(editor);

        _sessionChangeTrigger.OnNext(Unit.Default);
    }

    private void TrackEditorForAutosave(TabQueryEditorViewModel editor)
    {
        var subscription = editor.WhenAnyValue(e => e.SqlStatement)
            .Skip(1)
            .Subscribe(_ => _sessionChangeTrigger.OnNext(Unit.Default));

        _editorAutosaveSubscriptions[editor.Id] = subscription;
    }

    private void UntrackEditorForAutosave(TabQueryEditorViewModel editor)
    {
        if (_editorAutosaveSubscriptions.Remove(editor.Id, out var subscription))
            subscription.Dispose();
    }

    private void RestoreSessionOrAddDefaultEditor()
    {
        var sessionState = _sessionTabStore.Get(ConnectionSettings.Id);
        if (sessionState is null || sessionState.Editors.Count == 0)
        {
            AddQueryEditor();
            return;
        }

        foreach (var editorState in sessionState.Editors)
            RestoreQueryEditor(editorState);

        this.SelectedEditor = 0;
    }

    private void RestoreQueryEditor(EditorTabState state)
    {
        if (state.Name.StartsWith("Query ", StringComparison.Ordinal) &&
            int.TryParse(state.Name.AsSpan("Query ".Length), out var queryNumber))
            _countQueryEditors = Math.Max(_countQueryEditors, queryNumber);

        var filePath = state.File is not null && File.Exists(state.File) ? state.File : null;

        var queryEditor = new TabQueryEditorViewModel(
            ConnectionSettings,
            state.Name,
            filePath,
            canClose: true,
            this.ServiceProvider)
        {
            SqlStatement = state.SqlStatement,
            TextWasChanged = state.IsDirty
        };

        this.QueryEditors.Add(queryEditor);
    }

    private void AddQueryEditor(string? file = null)
    {
        var name = "";
        if (File.Exists(file))
        {
            name = Path.GetFileName(file);
        }
        else
        {
            _countQueryEditors++;
            name = $"Query {_countQueryEditors}";
        }

        var queryEditor = new TabQueryEditorViewModel(
            ConnectionSettings
            , name
            , file
            , canClose: true, 
            this.ServiceProvider);
        this.QueryEditors.Add(queryEditor);
        this.SelectedEditor = this.QueryEditors.Count - 1;
    }

    public void OpenQueryEditorWithScript(string sqlStatement)
    {
        AddQueryEditor();
        var queryEditor = QueryEditors[this.SelectedEditor];
        queryEditor.SqlStatement = sqlStatement;
    }

    private async Task LoadConnection()
    {
        try
        {
            await SchemaExplorer.InitializeSchemaNode();
            
            RootConnections.Clear();
            RootConnections.Add(SchemaExplorer.RootConnections);
        }
        catch (Exception ex)
        {
            RootConnections.Clear();
            await _dialogService.ShowMessageAsync(BuildConnectionErrorMessage(ex), "Connection error");
        }
    }

    private string BuildConnectionErrorMessage(Exception ex)
    {
        if (ConnectionSettings.DatabaseType == DatabaseType.SqlServer &&
            (ex.Message.Contains("Certificate failed chain validation", StringComparison.OrdinalIgnoreCase) ||
             ex.Message.Contains("Certificate name mismatch", StringComparison.OrdinalIgnoreCase)))
        {
            return "TLS validation failed for this SQL Server connection.\n\n" +
                   "If this is a legacy/local server that uses an untrusted certificate, edit the connection and enable 'Trust server certificate'.\n\n" +
                   ex.Message;
        }

        if (ConnectionSettings.DatabaseType == DatabaseType.MySql &&
            ex.Message.Contains("ssl", StringComparison.OrdinalIgnoreCase))
        {
            return "TLS validation failed for this MySQL connection.\n\n" +
                   "If the server uses TLS with a certificate that is not trusted on this machine, edit the connection and enable 'Trust server certificate'.\n\n" +
                   ex.Message;
        }

        return ex.Message;
    }
    
    public IConnectionSettings ConnectionSettings { get; }
    public ISchemaExplorer SchemaExplorer { get; }
    public Task Initialization { get; private set; }
    [Reactive] public int SelectedEditor { get; set; }
    [Reactive] public bool IsSchemaExplorerMinimized { get; set; }
    public ReactiveCommand<string?, Unit> AddQueryEditorCommand { get; }
    public ReactiveCommand<StyledElement, Unit> CreateTableCommand { get; }
    public ReactiveCommand<StyledElement, Unit> ImportFileCommand { get; }
    public ReactiveCommand<TabQueryEditorViewModel, bool> CloseTabQueryEditorCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ObservableCollection<SchemaNode> RootConnections { get; } = new();
    public ObservableCollection<TabQueryEditorViewModel> QueryEditors { get; } = new();
}
