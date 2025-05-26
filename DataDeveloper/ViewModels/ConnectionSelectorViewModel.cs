using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactiveUI;
using System.Reactive;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using DataDeveloper.Models;
using DataDeveloper.Services;
using DynamicData;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace DataDeveloper.ViewModels;

public class ConnectionSelectorViewModel : ViewModelBase
{
    private readonly AppDataFileService _fileService;
    private const string ConnectionsFolder = "connections";
    private const string FilePath = "connections.json";

    public ConnectionSelectorViewModel(AppDataFileService fileService)
    {
        _fileService = fileService;
        LoadConnections();
        
        AddCommand = ReactiveCommand.Create(() =>
        {
            var newConn = new ConnectionModel
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

        EditCommand = ReactiveCommand.Create<ConnectionModel>(
            (connectionModel) =>
            {
                IsEditing = true; 
                SelectedConnection = connectionModel;
            }
        );

        DeleteCommand = ReactiveCommand.CreateFromTask<StyledElement>(DeleteAsync);
        
        TestCommand = ReactiveCommand.Create(
            () => { /* Test connection logic */ },
            this.WhenAnyValue(x => x.SelectedConnection).Select(conn => conn is not null)
        );

        OkCommand = ReactiveCommand.CreateFromTask<StyledElement>(OkAsync,
            this.WhenAnyValue(x => x.SelectedConnection).Select(conn => conn is not null)
        );

        CancelCommand = ReactiveCommand.Create<StyledElement>(CancelAsync);      
    }

    private void CancelAsync(StyledElement element)
    {
        var window = element.GetParentWindow();
        window?.Close();
    }

    private async Task DeleteAsync(StyledElement control)
    {
        var connectionModel = control.DataContext as ConnectionModel;
        
        IsEditing = true; 
        SelectedConnection = connectionModel;
        var window = control.GetParentWindow();   
        var box = MessageBoxManager
            .GetMessageBoxStandard("Connection...", "Are you sure to delete this connection?", ButtonEnum.YesNo, Icon.Question);

        var result = await box.ShowAsPopupAsync(window);

        if (result == ButtonResult.Yes)
        {
            Connections.Remove(connectionModel);
            SaveConnection(Guid.Empty);
        }
    }

    public ObservableCollection<ConnectionModel> Connections { get; private set; } = new();

    private ConnectionModel? _selectedConnection;
    public ConnectionModel? SelectedConnection
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
    public ReactiveCommand<ConnectionModel, Unit> EditCommand { get; }
    public ReactiveCommand<StyledElement, Unit> ApplyCommand { get; }
    public ReactiveCommand<Unit, Unit> TestCommand { get; }
    public ReactiveCommand<StyledElement, Unit> CancelCommand { get; }
    public ReactiveCommand<StyledElement, Unit> DeleteCommand { get; }
    private async Task OkAsync(StyledElement element)
    {
        await ApplyAsync(element);
        var window = element.GetParentWindow();
        window?.Close(SelectedConnection);
    }

    private async Task ApplyAsync(StyledElement control)
    {
        if (SelectedConnection == null)
        {
            var window = control.GetParentWindow();            
            var box = MessageBoxManager
                .GetMessageBoxStandard("Connection...", "There is no connection selected to save!", ButtonEnum.Ok, Icon.Info);

            await box.ShowAsPopupAsync(window);

            return;
        }

        SaveConnection(SelectedConnection.Id);
    }

    private void SaveConnection(Guid connectionId)
    {
        _fileService.SaveJson(FilePath, Connections, ConnectionsFolder);
        
        IsEditing = false;
        LoadConnections();
        
        SelectedConnection = Connections.FirstOrDefault(c => c.Id == connectionId);
    }

    private void LoadConnections()
    {
        var sortedList  = _fileService.LoadJson<List<ConnectionModel>>(FilePath, ConnectionsFolder);
        Connections.Clear();
        if (sortedList is not null)
            Connections.AddRange(sortedList.OrderBy(s => s.Name));
    }
}
