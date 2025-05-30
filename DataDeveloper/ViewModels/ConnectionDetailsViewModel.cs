using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services;
using Dock.Model.ReactiveUI.Controls;
using DynamicData;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace DataDeveloper.ViewModels;

public class ConnectionDetailsViewModel : Tool
{
    private readonly IConnectionSettings _connectionSettings;
    private ISchemaExplorer _schemaExplorer;
    
    public ConnectionDetailsViewModel(IConnectionSettings connectionSettings)
    {
        _connectionSettings = connectionSettings;
        _schemaExplorer = _connectionSettings.GetSchemaExplorer();
        this.Initialization = LoadConnection();
        LoadItemsCommand = ReactiveCommand.CreateFromTask<SchemaNode>(LoadItems);
    }

    private async Task LoadItems(SchemaNode node)
    {
        if (node == null) return;

        switch (node.NodeType)
        {
            case NodeType.Columns:
                await _schemaExplorer.LoadTableColumnsAsync(node);
                break;
        }
    }

    public ReactiveCommand<SchemaNode, Unit> LoadItemsCommand { get; }
    public Task Initialization { get; private set; }
    public ObservableCollection<SchemaNode> RootConnections { get; } = new();
    private async Task LoadConnection()
    {
        await _schemaExplorer.InitializeSchemaNode();
        
        RootConnections.Clear();
        RootConnections.Add(_schemaExplorer.RootConnections);
    }
}

