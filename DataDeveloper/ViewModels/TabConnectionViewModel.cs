using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Enums;
using DataDeveloper.Models;
using DynamicData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

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
        
        LoadItemsCommand = ReactiveCommand.CreateFromTask<StyledElement>(LoadItems);
        AddQueryEditorCommand = ReactiveCommand.Create(AddQueryEditor);
        AddQueryEditor();
        this.WhenAnyValue(vm => vm.SelectedEditor).Subscribe(_ =>
        {
            QueryEditors[this.SelectedEditor].ShowCursorData();
        });
    }
    public IConnectionSettings ConnectionSettings { get; }
    public ISchemaExplorer SchemaExplorer { get; }
    public Task Initialization { get; private set; }
    [Reactive] public int SelectedEditor { get; set; }
    public ReactiveCommand<StyledElement, Unit> LoadItemsCommand { get; }
    public ReactiveCommand<Unit, Unit> AddQueryEditorCommand { get; }
    public ObservableCollection<SchemaNode> RootConnections { get; } = new();
    public ObservableCollection<TabQueryEditorViewModel> QueryEditors { get; } = new();

    private void AddQueryEditor()
    {
        _countQueryEditors++;
        this.QueryEditors.Add(new TabQueryEditorViewModel(ConnectionSettings, $"Query {_countQueryEditors}", canClose: true, this.ServiceProvider));
        this.SelectedEditor = this.QueryEditors.Count - 1;
    }

    private async Task LoadItems(StyledElement element)
    {
        var treeViewItem = element.FindLogicalAncestorOfType<TreeViewItem>();
        
        if (treeViewItem == null) return;

        var node = treeViewItem.DataContext as SchemaNode;
        
        if (node == null) return;
        
        switch (node.NodeType)
        {
            case NodeType.Columns:
                await SchemaExplorer.LoadTableColumnsAsync(node);
                break;
        }
        treeViewItem.IsExpanded = true;
    }

    private async Task LoadConnection()
    {
        await SchemaExplorer.InitializeSchemaNode();
        
        RootConnections.Clear();
        RootConnections.Add(SchemaExplorer.RootConnections);
    }
    
}