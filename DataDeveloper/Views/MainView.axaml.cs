using System;
using System.ComponentModel;
using System.IO;
using System.Reactive.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DataDeveloper.Docking;
using DataDeveloper.ViewModels;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using ReactiveUI;

namespace DataDeveloper.Views;

public partial class MainView : UserControl
{
    private MainWindowViewModel? _viewModel;
    private bool _isSyncingActiveConnection;

    public MainView()
    {
        InitializeComponent();
        DockableLogicalOwner.AdoptLayout(MainDock.Layout);
        if (MainDock.Factory is { } factory)
        {
            factory.DockableClosing += OnDockableClosing;
            factory.ActiveDockableChanged += OnActiveDockableChanged;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    // Closing a connection goes through the view model (it asks to save its query editors); when it confirms,
    // the connection leaves Connections and Dock removes the document itself.
    private void OnDockableClosing(object? sender, DockableClosingEventArgs e)
    {
        if (_viewModel is null || e.Dockable is not IDocument { Context: TabConnectionViewModel connection })
            return;

        e.Cancel = true;
        _viewModel.CloseTabConnectionCommand.Execute(connection).Subscribe();
    }

    private void OnActiveDockableChanged(object? sender, ActiveDockableChangedEventArgs e)
    {
        if (_viewModel is null || _isSyncingActiveConnection || e.Dockable is not IDocument { Context: TabConnectionViewModel connection })
            return;

        var index = _viewModel.Connections.IndexOf(connection);
        if (index < 0 || index == _viewModel.SelectedTabConnectionIndex)
            return;

        _isSyncingActiveConnection = true;
        try
        {
            _viewModel.SelectedTabConnectionIndex = index;
        }
        finally
        {
            _isSyncingActiveConnection = false;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.SelectedTabConnectionIndex) || _isSyncingActiveConnection)
            return;

        // Defer until Dock has generated the document for a newly added connection.
        Dispatcher.UIThread.Post(ActivateSelectedConnectionDocument, DispatcherPriority.Background);
    }

    private void ActivateSelectedConnectionDocument()
    {
        if (_viewModel is null || MainDock.Factory is not { } factory)
            return;

        var index = _viewModel.SelectedTabConnectionIndex;
        if (index < 0 || index >= _viewModel.Connections.Count ||
            factory.GetContainerFromItem(_viewModel.Connections[index]) is not IDockable document)
            return;

        _isSyncingActiveConnection = true;
        try
        {
            factory.SetActiveDockable(document);
            if (document.Owner is IDock owner)
                factory.SetFocusedDockable(owner, document);
        }
        finally
        {
            _isSyncingActiveConnection = false;
        }
    }

    private void OpenRecentMenuItem_OnSubmenuOpened(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || DataContext is not MainWindowViewModel viewModel)
            return;

        menuItem.Items.Clear();

        foreach (var filePath in viewModel.RecentFiles)
        {
            var item = new MenuItem { Header = Path.GetFileName(filePath) };
            item.Click += async (_, _) => await viewModel.OpenRecentFileCommand.Execute(filePath).ToTask();
            menuItem.Items.Add(item);
        }

        if (menuItem.Items.Count == 0)
            return;

        menuItem.Items.Add(new Separator());

        var clearItem = new MenuItem { Header = "Clear items" };
        clearItem.Click += async (_, _) => await viewModel.ClearRecentFilesCommand.Execute().ToTask();
        menuItem.Items.Add(clearItem);
    }
}
