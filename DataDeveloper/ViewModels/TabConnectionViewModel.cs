using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using DataDeveloper.Data;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Enums;
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

    public TabConnectionViewModel(IConnectionSettings connectionSettings, bool canClose, IServiceProvider serviceProvider) 
        : base(TabType.Connection, connectionSettings.Name, canClose, serviceProvider)
    {
        ConnectionSettings = connectionSettings;    
        SchemaExplorer = ConnectionSettings.GetSchemaExplorer();
        this.Initialization = LoadConnection();
        
        CloseTabQueryEditorCommand = ReactiveCommand.CreateFromTask<TabQueryEditorViewModel, bool>(CloseTabQueryEditor);
        AddQueryEditorCommand = ReactiveCommand.Create(AddQueryEditor);
        RefreshCommand = ReactiveCommand.CreateFromTask(Refresh);
        AddQueryEditor();
        this.WhenAnyValue(vm => vm.SelectedEditor).Subscribe(_ =>
        {
            if (this.SelectedEditor < 0) return;
            
            QueryEditors[this.SelectedEditor].ShowCursorData();
        });
    }

    private async Task Refresh()
    {
        await LoadConnection();
    }

    private async Task<bool> CloseTabQueryEditor(TabQueryEditorViewModel tabQueryEditor)
    {
        var remove = true;
        
        if (tabQueryEditor.TextWasChanged)
        {
            SelectedEditor = QueryEditors.IndexOf(tabQueryEditor);
            await Task.Delay(100);
            
            var dialog = ServiceProvider.GetService<IDialogService>();
            var result = await dialog.ShowDialogResult($"{tabQueryEditor.Name} was changed...\n\r\r\nDo you want to save that changes?");

            switch (result)
            {
                case DialogResult.Yes:
                    // TODO SAVE CHANGES - check if file was been saved to set remove to true, otherwise false
                    var fileName = $"{tabQueryEditor.Name}.sql";
                    await Task.Delay(100);
                    var filePath = await  dialog.ShowSaveFileDialogAsync(fileName);
                    if (filePath.IsNullOrEmpty())
                    {
                        remove = false;
                        break;
                    }
                    await File.WriteAllTextAsync(filePath, tabQueryEditor.SqlStatement);
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

    public IConnectionSettings ConnectionSettings { get; }
    public ISchemaExplorer SchemaExplorer { get; }
    public Task Initialization { get; private set; }
    [Reactive] public int SelectedEditor { get; set; }
    public ReactiveCommand<Unit, Unit> AddQueryEditorCommand { get; }
    public ReactiveCommand<TabQueryEditorViewModel, bool> CloseTabQueryEditorCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ObservableCollection<SchemaNode> RootConnections { get; } = new();
    public ObservableCollection<TabQueryEditorViewModel> QueryEditors { get; } = new();

    private void AddQueryEditor()
    {
        _countQueryEditors++;
        var queryEditor = new TabQueryEditorViewModel(
            ConnectionSettings
            , name: $"Query {_countQueryEditors}"
            , canClose: true, 
            this.ServiceProvider);
        this.QueryEditors.Add(queryEditor);
        this.SelectedEditor = this.QueryEditors.Count - 1;
    }

    private async Task LoadConnection()
    {
        await SchemaExplorer.InitializeSchemaNode();
        
        RootConnections.Clear();
        RootConnections.Add(SchemaExplorer.RootConnections);
    }
    
}