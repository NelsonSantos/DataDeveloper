using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class DockMenuBuilderTests
{
    [Fact]
    public void SelectRecentConnections_SkipsOpenAndDeletedConnections_KeepingRecentOrder()
    {
        var open = Connection("open");
        var older = Connection("older");
        var newer = Connection("newer");
        var deletedId = Guid.NewGuid();

        var recent = DockMenuBuilder.SelectRecentConnections(
            [newer.Id, open.Id, deletedId, older.Id],
            [older, open, newer],
            [open.Id]);

        Assert.Equal(["newer", "older"], recent.Select(connection => connection.Name));
    }

    [Fact]
    public void SelectRecentConnections_OffersAtMostFive()
    {
        var saved = Enumerable.Range(1, 7).Select(index => Connection($"c{index}")).ToList();

        var recent = DockMenuBuilder.SelectRecentConnections(saved.Select(connection => connection.Id), saved, []);

        Assert.Equal(["c1", "c2", "c3", "c4", "c5"], recent.Select(connection => connection.Name));
    }

    [AvaloniaFact]
    public void Populate_ListsOpenConnectionsUnderATitle_CheckingTheActiveOne()
    {
        var menu = new NativeMenu();

        DockMenuBuilder.Populate(menu, ["NassServer", "Integration SQLite"], 1, [], canLoadOrAddConnection: true, new RecordingActions());

        var title = Assert.IsType<NativeMenuItem>(menu.Items[0]);
        Assert.Equal("Open connections", title.Header);
        Assert.False(title.IsEnabled);
        var connections = menu.Items.Skip(1).Take(2).Cast<NativeMenuItem>().ToList();
        Assert.Equal(["NassServer", "Integration SQLite"], connections.Select(item => item.Header));
        Assert.Equal([false, true], connections.Select(item => item.IsChecked));
        Assert.IsType<NativeMenuItemSeparator>(menu.Items[3]);
        Assert.Equal(["Recent connections", "New query", "Load/Add connection…"], Headers(menu.Items.Skip(4)));
    }

    [AvaloniaFact]
    public void Populate_WithoutConnections_DisablesNewQueryAndRecentConnections()
    {
        var menu = new NativeMenu();

        DockMenuBuilder.Populate(menu, [], -1, [], canLoadOrAddConnection: true, new RecordingActions());

        Assert.Equal(["Recent connections", "New query", "Load/Add connection…"], Headers(menu.Items));
        Assert.Equal([false, false, true], menu.Items.Cast<NativeMenuItem>().Select(item => item.IsEnabled));
    }

    [AvaloniaFact]
    public void Populate_RecentConnectionsSubmenu_OpensTheConnectionOrClearsTheList()
    {
        var menu = new NativeMenu();
        var actions = new RecordingActions();
        var recent = Connection("my-hub");

        DockMenuBuilder.Populate(menu, [], -1, [recent], canLoadOrAddConnection: true, actions);

        var submenu = ((NativeMenuItem)menu.Items[0]).Menu!;
        Assert.Equal(["my-hub", "Clear menu"], Headers(submenu.Items));
        ((NativeMenuItem)submenu.Items[0]).Command!.Execute(null);
        ((NativeMenuItem)submenu.Items[2]).Command!.Execute(null);
        Assert.Equal(["open my-hub", "clear"], actions.Calls);
    }

    [AvaloniaFact]
    public void Populate_ItemsRunTheirActions()
    {
        var menu = new NativeMenu();
        var actions = new RecordingActions();

        DockMenuBuilder.Populate(menu, ["a", "b"], 0, [], canLoadOrAddConnection: true, actions);
        foreach (var item in menu.Items.OfType<NativeMenuItem>().Where(item => item.Command is not null))
            item.Command!.Execute(null);

        Assert.Equal(["activate 0", "activate 1", "new query", "load or add"], actions.Calls);
    }

    [AvaloniaFact]
    public void Populate_ReplacesThePreviousItems()
    {
        var menu = new NativeMenu();
        DockMenuBuilder.Populate(menu, ["a", "b"], 0, [], canLoadOrAddConnection: true, new RecordingActions());

        DockMenuBuilder.Populate(menu, [], -1, [], canLoadOrAddConnection: true, new RecordingActions());

        Assert.Equal(3, menu.Items.Count);
    }

    private static IEnumerable<string?> Headers(IEnumerable<NativeMenuItemBase> items) =>
        items.OfType<NativeMenuItem>().Where(item => item is not NativeMenuItemSeparator).Select(item => item.Header);

    private static ConnectionSettings Connection(string name) =>
        new SqLiteConnectionSettings { Id = Guid.NewGuid(), Name = name, DatabaseType = DatabaseType.SqLite };

    private sealed class RecordingActions : IDockMenuActions
    {
        public List<string> Calls { get; } = new();

        public void ActivateConnection(int index) => Calls.Add($"activate {index}");
        public void OpenRecentConnection(ConnectionSettings connectionSettings) => Calls.Add($"open {connectionSettings.Name}");
        public void ClearRecentConnections() => Calls.Add("clear");
        public void NewQuery() => Calls.Add("new query");
        public void LoadOrAddConnection() => Calls.Add("load or add");
    }
}
