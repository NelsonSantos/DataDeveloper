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
using Dock.Model.ReactiveUI.Controls;
using DynamicData;
using ReactiveUI;

namespace DataDeveloper.ViewModels;

public class ConnectionDetailsViewModel : Tool
{
    private readonly IConnectionSettings _connectionSettings;
    public ISchemaExplorer SchemaExplorer { get; }
    
    public ConnectionDetailsViewModel(IConnectionSettings connectionSettings)
    {
        _connectionSettings = connectionSettings;
        SchemaExplorer = _connectionSettings.GetSchemaExplorer();
        this.Initialization = LoadConnection();
        LoadItemsCommand = ReactiveCommand.CreateFromTask<StyledElement>(LoadItems);
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

    public ReactiveCommand<StyledElement, Unit> LoadItemsCommand { get; }
    public Task Initialization { get; private set; }
    public ObservableCollection<SchemaNode> RootConnections { get; } = new();
    private async Task LoadConnection()
    {
        await SchemaExplorer.InitializeSchemaNode();
        
        RootConnections.Clear();
        RootConnections.Add(SchemaExplorer.RootConnections);
    }
}

