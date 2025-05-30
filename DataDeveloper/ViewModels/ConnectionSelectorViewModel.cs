using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactiveUI;
using System.Reactive;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using DataDeveloper.Core;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.JsonConverters;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Data.Services;
using DataDeveloper.Services;
using DynamicData;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace DataDeveloper.ViewModels;

public class ConnectionSelectorViewModel : ViewModelBase
{
    private readonly AppDataFileService _fileService;
    private readonly DatabaseProviderFactoryService _databaseProviderFactoryService;
    private const string ConnectionsFolder = "connections";
    private const string FilePath = "connections.json";

    public ConnectionSelectorViewModel(AppDataFileService fileService, DatabaseProviderFactoryService databaseProviderFactoryService)
    {
        _fileService = fileService;
        _databaseProviderFactoryService = databaseProviderFactoryService;
        LoadConnections();
        
        AddCommand = ReactiveCommand.Create(() =>
        {
            var newConn = new SqlServerConnectionSettings
            {
                Id = Guid.NewGuid(),
                Name = "New Connection name",
                Server = "",
                Database = "",
                User = "",
                Password = ""
            };
            Connections.Add(newConn);
            SelectedConnection = newConn;
            IsEditing = true;
        });

        ApplyCommand = ReactiveCommand.CreateFromTask<StyledElement>(ApplyAsync,
            this.WhenAnyValue(x => x.IsEditing)
        );

        EditCommand = ReactiveCommand.Create<ConnectionSettings>(
            (connectionModel) =>
            {
                IsEditing = true; 
                SelectedConnection = connectionModel;
            }
        );

        DeleteCommand = ReactiveCommand.CreateFromTask<StyledElement>(DeleteAsync);
        
        TestCommand = ReactiveCommand.CreateFromTask<StyledElement>(TestConnection,
            this.WhenAnyValue(x => x.SelectedConnection).Select(conn => conn is not null)
        );

        OkCommand = ReactiveCommand.CreateFromTask<StyledElement>(OkAsync,
            this.WhenAnyValue(x => x.SelectedConnection).Select(conn => conn is not null)
        );

        CancelCommand = ReactiveCommand.Create<StyledElement>(CancelAsync);      
    }

    private async Task TestConnection(StyledElement element)
    {
        var databaseProvider = _databaseProviderFactoryService.GetDatabaseProvider(SelectedConnection);
        var result = databaseProvider.TestConnection();
        await this.ShowDialogAsync(
            element
            , "Connection..."
            , result.Success ? result.ResultMessage : $"Could not connect to database\r\n\r\n{result.ResultMessage}"
            , ButtonEnum.Ok
            , result.Success ? Icon.Success : Icon.Error);
    }

    private void CancelAsync(StyledElement element)
    {
        var window = element.GetParentWindow();
        window?.Close();
    }

    private async Task DeleteAsync(StyledElement element)
    {
        var connectionModel = element.DataContext as ConnectionSettings;
        
        IsEditing = true; 
        SelectedConnection = connectionModel;

        var result = await this.ShowDialogAsync(element, "Connection...", "Are you sure to delete this connection?", ButtonEnum.YesNo, Icon.Question);

        if (result == ButtonResult.Yes)
        {
            Connections.Remove(connectionModel);
            SaveConnection(Guid.Empty);
        }
    }

    public ObservableCollection<ConnectionSettings> Connections { get; private set; } = new();

    private ConnectionSettings? _selectedConnection;
    public ConnectionSettings? SelectedConnection
    {
        get => _selectedConnection;
        set => this.RaiseAndSetIfChanged(ref _selectedConnection, value);
    }

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set => this.RaiseAndSetIfChanged(ref _isEditing, value);
    }    
    public SqlConnectionInfo? ConnectionInfo { get; private set; }

    public ReactiveCommand<StyledElement, Unit> OkCommand { get; }
    public ReactiveCommand<Unit, Unit> AddCommand { get; }
    public ReactiveCommand<ConnectionSettings, Unit> EditCommand { get; }
    public ReactiveCommand<StyledElement, Unit> ApplyCommand { get; }
    public ReactiveCommand<StyledElement, Unit> TestCommand { get; }
    public ReactiveCommand<StyledElement, Unit> CancelCommand { get; }
    public ReactiveCommand<StyledElement, Unit> DeleteCommand { get; }
    private async Task OkAsync(StyledElement element)
    {
        await ApplyAsync(element);
        var window = element.GetParentWindow();
        window?.Close(SelectedConnection);
    }

    private async Task ApplyAsync(StyledElement element)
    {
        if (SelectedConnection == null)
        {
            await this.ShowDialogAsync(element, "Connection...", "There is no connection selected to save!", ButtonEnum.Ok, Icon.Info);
            return;
        }

        SaveConnection(SelectedConnection.Id);
    }

    private async Task<ButtonResult> ShowDialogAsync(StyledElement element, string title, string messagge, ButtonEnum button, Icon icon)
    {
        var window = element.GetParentWindow();            
        var box = MessageBoxManager
            .GetMessageBoxStandard(title, messagge, button, icon);

        return await box.ShowAsPopupAsync(window);
    }

    private void SaveConnection(Guid connectionId)
    {
        _fileService.SaveJson(FilePath, Connections, ConnectionsFolder, new ConnectionSettingsConverter());
        
        IsEditing = false;
        LoadConnections();
        
        SelectedConnection = Connections.FirstOrDefault(c => c.Id == connectionId);
    }

    private void LoadConnections()
    {
        var sortedList  = _fileService.LoadJson<List<ConnectionSettings>>(FilePath, ConnectionsFolder, new ConnectionSettingsConverter());
        Connections.Clear();
        if (sortedList is not null)
            Connections.AddRange(sortedList.OrderBy(s => s.Name));
    }
}
