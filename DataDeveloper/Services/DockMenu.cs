using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using DataDeveloper.Data.Models;
using DataDeveloper.Interfaces;
using DataDeveloper.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.Services;

/// <summary>
/// The menu of Data Developer's icon in the macOS Dock. It follows the main window the user activated last (the app
/// can have several) and is rebuilt whenever that window's connections, active connection or recent connections
/// change.
/// </summary>
public sealed class DockMenu : IDockMenuActions
{
    private static DockMenu? _instance;

    private readonly NativeMenu _menu = new();
    private readonly IConnectionSettingsRepository _connectionSettingsRepository;
    private Window? _window;
    private MainWindowViewModel? _viewModel;
    private IReadOnlyList<ConnectionSettings> _recentConnections = [];

    private DockMenu(IConnectionSettingsRepository connectionSettingsRepository)
    {
        _connectionSettingsRepository = connectionSettingsRepository;
    }

    /// <summary>Attaches the Dock menu to the app; only macOS has one.</summary>
    public static void Install(Application application, IServiceProvider serviceProvider)
    {
        if (!OperatingSystem.IsMacOS() || _instance is not null)
            return;

        _instance = new DockMenu(serviceProvider.GetRequiredService<IConnectionSettingsRepository>());
        NativeDock.SetMenu(application, _instance._menu);
        _instance.Rebuild();
    }

    /// <summary>Makes the menu follow <paramref name="window"/> (call when it is activated).</summary>
    public static void Follow(Window window, MainWindowViewModel viewModel) => _instance?.Attach(window, viewModel);

    /// <summary>Stops following <paramref name="window"/> (call when it closes).</summary>
    public static void Forget(Window window)
    {
        if (_instance is { } instance && ReferenceEquals(instance._window, window))
            instance.Attach(null, null);
    }

    private void Attach(Window? window, MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_window, window) && ReferenceEquals(_viewModel, viewModel))
            return;

        if (_viewModel is not null)
        {
            _viewModel.Connections.CollectionChanged -= OnConnectionsChanged;
            _viewModel.RecentConnectionIds.CollectionChanged -= OnConnectionsChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _window = window;
        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.Connections.CollectionChanged += OnConnectionsChanged;
            _viewModel.RecentConnectionIds.CollectionChanged += OnConnectionsChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        RefreshRecentConnections();
        Rebuild();
    }

    private void OnConnectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshRecentConnections();
        Rebuild();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedTabConnectionIndex))
            Rebuild();
    }

    // Reading the saved connections hits the settings database, so it only happens when the open or recent
    // connections change, not on every tab switch.
    private void RefreshRecentConnections()
    {
        if (_viewModel is null || _viewModel.RecentConnectionIds.Count == 0)
        {
            _recentConnections = [];
            return;
        }

        try
        {
            _recentConnections = DockMenuBuilder.SelectRecentConnections(
                _viewModel.RecentConnectionIds,
                _connectionSettingsRepository.LoadAll(),
                _viewModel.Connections.Select(connection => connection.ConnectionSettings.Id));
        }
        catch (Exception exception)
        {
            _recentConnections = [];
            UnhandledErrorReporter.Log(exception, "DockMenu");
        }
    }

    private void Rebuild()
    {
        var connections = _viewModel?.Connections.Select(connection => connection.Name).ToList() ?? [];
        DockMenuBuilder.Populate(
            _menu,
            connections,
            _viewModel?.SelectedTabConnectionIndex ?? -1,
            _recentConnections,
            canLoadOrAddConnection: _window is not null,
            this);
    }

    public void ActivateConnection(int index)
    {
        if (_viewModel is null || index < 0 || index >= _viewModel.Connections.Count)
            return;

        _window?.Activate();
        _viewModel.SelectedTabConnectionIndex = index;
    }

    public void OpenRecentConnection(ConnectionSettings connectionSettings)
    {
        if (_viewModel is not { } viewModel)
            return;

        _window?.Activate();
        _ = RunAsync(() => viewModel.OpenSavedConnectionAsync(connectionSettings));
    }

    public void ClearRecentConnections() => _viewModel?.ClearRecentConnections();

    public void NewQuery()
    {
        if (_viewModel is not { } viewModel)
            return;

        _window?.Activate();
        _ = RunAsync(() => viewModel.NewQueryTabCommand.Execute().ToTask());
    }

    public void LoadOrAddConnection()
    {
        if (_viewModel is not { } viewModel || _window is not { } window)
            return;

        window.Activate();
        _ = RunAsync(() => viewModel.NewConnectionCommand.Execute(window).ToTask());
    }

    // Dock menu clicks have no caller to report to, so failures (e.g. the password could not be read) are shown here.
    private static async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            UnhandledErrorReporter.Report(exception, "DockMenu");
        }
    }
}
