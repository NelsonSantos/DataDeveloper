using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using DataDeveloper.Data.Models;
using System.Windows.Input;

namespace DataDeveloper.Services;

/// <summary>What the macOS Dock menu shows and what its items do.</summary>
public interface IDockMenuActions
{
    void ActivateConnection(int index);
    void OpenRecentConnection(ConnectionSettings connectionSettings);
    void ClearRecentConnections();
    void NewQuery();
    void LoadOrAddConnection();
}

/// <summary>
/// Builds the items Data Developer adds to its icon's menu in the macOS Dock: the open connections (the active one
/// checked), recent connections that are not open, New query and Load/Add connection. macOS adds its own items
/// (windows, Options, Hide, Quit) around them.
/// </summary>
public static class DockMenuBuilder
{
    public const int MaxRecentConnections = 5;

    /// <summary>
    /// The recent connections worth offering: saved ones (deleted connections drop out), not already open, most
    /// recent first.
    /// </summary>
    public static IReadOnlyList<ConnectionSettings> SelectRecentConnections(
        IEnumerable<Guid> recentConnectionIds,
        IEnumerable<ConnectionSettings> savedConnections,
        IEnumerable<Guid> openConnectionIds,
        int maxCount = MaxRecentConnections)
    {
        var saved = savedConnections.GroupBy(connection => connection.Id).ToDictionary(group => group.Key, group => group.First());
        var open = openConnectionIds.ToHashSet();

        return recentConnectionIds
            .Where(id => !open.Contains(id))
            .Select(id => saved.GetValueOrDefault(id))
            .OfType<ConnectionSettings>()
            .Take(maxCount)
            .ToList();
    }

    public static void Populate(
        NativeMenu menu,
        IReadOnlyList<string> openConnectionNames,
        int activeConnectionIndex,
        IReadOnlyList<ConnectionSettings> recentConnections,
        bool canLoadOrAddConnection,
        IDockMenuActions actions)
    {
        menu.Items.Clear();

        if (openConnectionNames.Count > 0)
        {
            // A disabled item works as the section title, as in other macOS Dock menus.
            menu.Items.Add(new NativeMenuItem("Open connections") { IsEnabled = false });
            for (var index = 0; index < openConnectionNames.Count; index++)
            {
                var connectionIndex = index;
                menu.Items.Add(new NativeMenuItem(openConnectionNames[index])
                {
                    ToggleType = MenuItemToggleType.CheckBox,
                    IsChecked = index == activeConnectionIndex,
                    Command = new MenuCommand(() => actions.ActivateConnection(connectionIndex))
                });
            }

            menu.Items.Add(new NativeMenuItemSeparator());
        }

        menu.Items.Add(BuildRecentConnectionsItem(recentConnections, actions));

        menu.Items.Add(ActionItem("New query", openConnectionNames.Count > 0, actions.NewQuery));
        menu.Items.Add(ActionItem("Load/Add connection…", canLoadOrAddConnection, actions.LoadOrAddConnection));
    }

    // An item with a command takes its enabled state from the command, so a disabled item gets none.
    private static NativeMenuItem ActionItem(string header, bool isEnabled, Action action) =>
        new(header) { IsEnabled = isEnabled, Command = isEnabled ? new MenuCommand(action) : null };

    private static NativeMenuItem BuildRecentConnectionsItem(IReadOnlyList<ConnectionSettings> recentConnections, IDockMenuActions actions)
    {
        var item = new NativeMenuItem("Recent connections");
        if (recentConnections.Count == 0)
        {
            item.IsEnabled = false;
            return item;
        }

        var submenu = new NativeMenu();
        foreach (var connection in recentConnections)
        {
            submenu.Items.Add(new NativeMenuItem(connection.Name)
            {
                Command = new MenuCommand(() => actions.OpenRecentConnection(connection))
            });
        }

        submenu.Items.Add(new NativeMenuItemSeparator());
        submenu.Items.Add(new NativeMenuItem("Clear menu") { Command = new MenuCommand(actions.ClearRecentConnections) });

        item.Menu = submenu;
        return item;
    }

    // A plain synchronous command: a ReactiveCommand would report its result through the UI scheduler, which a
    // menu click does not need.
    private sealed class MenuCommand(Action action) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => action();
    }
}
