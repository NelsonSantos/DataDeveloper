using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Interfaces;
using DataDeveloper.ViewModels.Docks;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using Dock.Model.ReactiveUI;
using Dock.Model.ReactiveUI.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;

namespace DataDeveloper.Services;

public class DockFactoryService : Factory, IAuxFactory
{
    private int _countSqlEditors = 0;
    private RootDock _homeView;
    private ConnectionDocumentDock _connectionDocumentDock;
    
    public DockFactoryService()
    {
        AddNewConnectionCommand = ReactiveCommand.CreateFromTask<IConnectionSettings>(AddConnection);
    }

    private async Task AddConnection(IConnectionSettings connectionSettings)
    {
        await Task.Delay(100);
        
        if (_connectionDocumentDock == null)
            _connectionDocumentDock = new ConnectionDocumentDock(this, connectionSettings);

        _homeView.ActiveDockable = _connectionDocumentDock;

        if (_homeView.VisibleDockables == null)
        {
            _homeView.VisibleDockables = CreateList<IDockable>(_connectionDocumentDock);
        }
        else
        {
            _connectionDocumentDock.AddNewConnection(connectionSettings);
        }
    }

    public ReactiveCommand<IConnectionSettings, Unit> AddNewConnectionCommand { get; }
    private Task _initializationTask;
    public override IRootDock CreateLayout()
    {
        this.DockableWillBeClosed += OnDockableWillBeClosed;

        _homeView = new RootDock
        {
            Id = "Home",
            Title = "Home",
            IsCollapsable = false,
            CanFloat = false
        };
        
        var rootDock = CreateRootDock();

        rootDock.IsCollapsable = false;
        rootDock.ActiveDockable = _homeView;
        rootDock.DefaultDockable = _homeView;
        rootDock.VisibleDockables = CreateList<IDockable>(_homeView);

        return rootDock;
    }

    private async Task<bool> OnDockableWillBeClosed(DockableWillBeClosedEventArgs e)
    {
        return await CanCloseDocument(e.Dockable).ConfigureAwait(true);
    }
    
    private async Task<bool> CanCloseDocument(IDockable? dockable)
    {
        if (dockable == null)
            return false;
        
        // Localiza o DocumentDock pai
        if (dockable.Owner is DocumentDock parent)
        {
            var count = parent.VisibleDockables?.OfType<Document>().Count() ?? 0;

            // É o último documento aberto?
            bool isLast = count <= 1;

            // Exibe uma MessageBox de confirmação
            var result= await ShowConfirmationAsync(isLast);

            return result; // true para permitir fechar, false para cancelar
        }

        return true;
    }

    private async Task<bool> ShowConfirmationAsync(bool isLast)
    {
        string message = isLast
            ? "Esta é a última aba aberta. Deseja realmente fechá-la?"
            : "Deseja fechar esta aba?";

        var box = MessageBoxManager
            .GetMessageBoxStandard("Fechar Documento", message,
                ButtonEnum.YesNo, Icon.Question);

        var result = await box.ShowAsync();

        return result == ButtonResult.Yes;
    }

    private Window? GetHostWindow()
    {
        // Tenta encontrar uma janela associada
        var window = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows.FirstOrDefault()
            : null;
        return window;
    }
}