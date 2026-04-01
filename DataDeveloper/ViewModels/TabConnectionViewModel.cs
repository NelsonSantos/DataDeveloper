using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services;
using DataDeveloper.Enums;
using DataDeveloper.EventAggregators;
using DataDeveloper.Interfaces;
using DataDeveloper.Models;
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
    private readonly IEventAggregatorService _eventAggregatorService;
    
    public TabConnectionViewModel(IConnectionSettings connectionSettings, bool canClose, IServiceProvider serviceProvider) 
        : base(TabType.Connection, connectionSettings.Name, canClose, serviceProvider)
    {
        ConnectionSettings = connectionSettings;    
        SchemaExplorer = ConnectionSettings.GetSchemaExplorer();
        _dialogService = ServiceProvider.GetRequiredService<IDialogService>();
        _eventAggregatorService = ServiceProvider.GetRequiredService<IEventAggregatorService>();
        
        this.Initialization = LoadConnection();

        CloseTabQueryEditorCommand = ReactiveCommand.CreateFromTask<TabQueryEditorViewModel, bool>(tab => CloseTabQueryEditor(tab));
        AddQueryEditorCommand = ReactiveCommand.Create<string?>(AddQueryEditor);
        RefreshCommand = ReactiveCommand.CreateFromTask(Refresh);
        AddQueryEditor();
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

    public async Task ExecuteBackgroundStatementAsync(string statement, bool refreshSchema = false)
    {
        try
        {
            await using var connection = ConnectionSettings.GetDatabaseProvider().GetConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync();

            if (refreshSchema || StatementExecutionClassifier.RequiresSchemaRefresh(statement))
                await SchemaExplorer.RefreshSchemaObjectAsync(statement);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync(ex.Message, "Execution error");
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

    public async Task<bool> CloseTabQueryEditor(TabQueryEditorViewModel tabQueryEditor, bool showDialog = true)
    {
        var remove = true;
        
        if (tabQueryEditor.TextWasChanged)
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
        queryEditor.TextWasChanged = false;
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
    public ReactiveCommand<TabQueryEditorViewModel, bool> CloseTabQueryEditorCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ObservableCollection<SchemaNode> RootConnections { get; } = new();
    public ObservableCollection<TabQueryEditorViewModel> QueryEditors { get; } = new();
}
