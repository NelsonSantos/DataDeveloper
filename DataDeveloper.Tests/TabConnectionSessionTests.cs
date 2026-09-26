using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DataDeveloper.Core;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Data.Services;
using DataDeveloper.Enums;
using DataDeveloper.EventAggregators;
using DataDeveloper.Interfaces;
using DataDeveloper.Models;
using DataDeveloper.Services;
using DataDeveloper.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DataDeveloper.Tests;

public class TabConnectionSessionTests
{
    [Fact]
    public void SessionTabStore_SaveThenGet_RoundTripsEditors()
    {
        var (store, _) = CreateFileBackedStore();
        var connectionId = Guid.NewGuid();
        try
        {
            var editors = new List<EditorTabState>
            {
                new() { Name = "Query 1", File = null, SqlStatement = "select 1", IsDirty = true },
                new() { Name = "script.sql", File = "/tmp/script.sql", SqlStatement = "select 2", IsDirty = false }
            };

            store.Save(connectionId, editors);
            var loaded = store.Get(connectionId);

            Assert.NotNull(loaded);
            Assert.Equal(connectionId, loaded!.ConnectionId);
            Assert.Equal(2, loaded.Editors.Count);
            Assert.Equal("select 1", loaded.Editors[0].SqlStatement);
            Assert.True(loaded.Editors[0].IsDirty);
            Assert.Equal("script.sql", loaded.Editors[1].Name);
            Assert.False(loaded.Editors[1].IsDirty);
        }
        finally
        {
            store.Remove(connectionId);
        }
    }

    [Fact]
    public void SessionTabStore_SaveThenGet_RoundTripsPanelLayouts()
    {
        var (store, _) = CreateFileBackedStore();
        var connectionId = Guid.NewGuid();
        try
        {
            store.Save(
                connectionId,
                new List<EditorTabState> { new() { Name = "Query 1", SqlStatement = "select 1", Results = new PanelLayoutState(0.55, true) } },
                new PanelLayoutState(0.2, false));

            var loaded = store.Get(connectionId);

            Assert.Equal(new PanelLayoutState(0.2, false), loaded!.SchemaExplorer);
            Assert.Equal(new PanelLayoutState(0.55, true), Assert.Single(loaded.Editors).Results);
        }
        finally
        {
            store.Remove(connectionId);
        }
    }

    [Fact]
    public void SessionTabStore_Save_OverwritesPreviousState()
    {
        var (store, _) = CreateFileBackedStore();
        var connectionId = Guid.NewGuid();
        try
        {
            store.Save(connectionId, new List<EditorTabState> { new() { Name = "Query 1", SqlStatement = "old" } });
            store.Save(connectionId, new List<EditorTabState> { new() { Name = "Query 1", SqlStatement = "new" } });

            var loaded = store.Get(connectionId);

            Assert.NotNull(loaded);
            var editor = Assert.Single(loaded!.Editors);
            Assert.Equal("new", editor.SqlStatement);
        }
        finally
        {
            store.Remove(connectionId);
        }
    }

    [Fact]
    public void SessionTabStore_Remove_DeletesState()
    {
        var (store, _) = CreateFileBackedStore();
        var connectionId = Guid.NewGuid();

        store.Save(connectionId, new List<EditorTabState> { new() { Name = "Query 1", SqlStatement = "x" } });
        store.Remove(connectionId);

        Assert.Null(store.Get(connectionId));
    }

    [Fact]
    public void SessionTabStore_Get_UnknownConnection_ReturnsNull()
    {
        var (store, _) = CreateFileBackedStore();

        Assert.Null(store.Get(Guid.NewGuid()));
    }

    [Fact]
    public void NewConnection_NoPriorSession_CreatesSingleDefaultEditor()
    {
        using var context = CreateConnectionContext();

        var editor = Assert.Single(context.ViewModel.QueryEditors);
        Assert.Equal("Query 1", editor.Name);
        Assert.Equal(string.Empty, editor.SqlStatement);
    }

    [Fact]
    public void NewConnection_WithPriorSession_RestoresEditorsAndContent()
    {
        var connectionId = Guid.NewGuid();
        var sessionStore = new InMemorySessionTabStore();
        sessionStore.Save(connectionId, new List<EditorTabState>
        {
            new() { Name = "Query 1", File = null, SqlStatement = "select * from customers", IsDirty = true },
            new() { Name = "missing.sql", File = "/path/that/does/not/exist.sql", SqlStatement = "select * from orders", IsDirty = true }
        });

        using var context = CreateConnectionContext(connectionId, sessionStore);

        Assert.Equal(2, context.ViewModel.QueryEditors.Count);
        Assert.Equal("select * from customers", context.ViewModel.QueryEditors[0].SqlStatement);
        Assert.True(context.ViewModel.QueryEditors[0].TextWasChanged);
        Assert.Equal("select * from orders", context.ViewModel.QueryEditors[1].SqlStatement);
        Assert.Null(context.ViewModel.QueryEditors[1].File);
    }

    [Fact]
    public async Task CloseTabQueryEditor_PersistSession_SkipsSaveDialogAndRemovesDirtyTab()
    {
        using var context = CreateConnectionContext();
        var editor = context.ViewModel.QueryEditors[0];
        editor.SqlStatement = "select 1";

        var removed = await context.ViewModel.CloseTabQueryEditor(editor, showDialog: false, persistSession: true);

        Assert.True(removed);
        Assert.Empty(context.ViewModel.QueryEditors);
        Assert.Equal(0, context.DialogService.SaveChangesPromptCount);
    }

    [Fact]
    public async Task CloseTabQueryEditor_WithoutPersistSession_StillPromptsToSaveDirtyTab()
    {
        using var context = CreateConnectionContext();
        var editor = context.ViewModel.QueryEditors[0];
        editor.SqlStatement = "select 1";
        context.DialogService.NextSaveChangesResult = DialogResult.No;

        var removed = await context.ViewModel.CloseTabQueryEditor(editor, showDialog: true, persistSession: false);

        Assert.True(removed);
        Assert.Equal(1, context.DialogService.SaveChangesPromptCount);
    }

    [Fact]
    public async Task OpenQueryEditorWithScript_MarksNewTabDirty_SoClosingWithoutSavingPromptsInsteadOfSilentlyDiscarding()
    {
        // Regression test: "DDL create to new query" used to open the generated script
        // already marked as clean (TextWasChanged = false), even though it has no backing
        // file. That made CloseTabQueryEditor skip the save prompt entirely and silently
        // remove the tab.
        using var context = CreateConnectionContext();
        context.ViewModel.OpenQueryEditorWithScript("create table foo (id int)");
        var editor = context.ViewModel.QueryEditors[context.ViewModel.SelectedEditor];

        Assert.True(editor.TextWasChanged);
        Assert.Null(editor.File);

        var removed = await context.ViewModel.CloseTabQueryEditor(editor, showDialog: false);

        // The Save-As dialog is "cancelled" by RecordingDialogService (returns null), so the
        // tab must not be discarded silently.
        Assert.False(removed);
        Assert.Contains(editor, context.ViewModel.QueryEditors);
    }

    [Fact]
    public async Task SaveCurrentEditorTab_OnUntitledTabWithNoFile_ShowsSaveAsDialogInsteadOfClosingTab()
    {
        // Regression test: MainWindowViewModel.SaveCurrentEditorTab used to route any tab
        // without an existing file on disk into CloseTabQueryEditor instead of SaveChanges,
        // so pressing Ctrl+S / using the Save menu on an untitled tab (e.g. one opened via
        // "DDL create to new query") silently closed it instead of prompting for a file name.
        using var context = CreateConnectionContext();
        context.ViewModel.OpenQueryEditorWithScript("create table foo (id int)");
        var editor = context.ViewModel.QueryEditors[context.ViewModel.SelectedEditor];

        var mainWindowViewModel = new MainWindowViewModel(new MainWindowServiceProviderStub());
        mainWindowViewModel.Connections.Add(context.ViewModel);

        var saveCurrentEditorTab = typeof(MainWindowViewModel).GetMethod(
            "SaveCurrentEditorTab",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(saveCurrentEditorTab);

        var saveTask = Assert.IsAssignableFrom<Task>(saveCurrentEditorTab!.Invoke(mainWindowViewModel, new object[] { false }));
        await saveTask;

        Assert.Equal(1, context.DialogService.ShowSaveFileDialogCallCount);
        Assert.Contains(editor, context.ViewModel.QueryEditors);
    }

    [Fact]
    public void EditorsByRecentUse_FollowsSelectionAndDropsClosedEditors()
    {
        using var context = CreateConnectionContext();
        var first = context.ViewModel.QueryEditors[0];
        context.ViewModel.OpenQueryEditorWithScript("select 2");
        context.ViewModel.OpenQueryEditorWithScript("select 3");
        var (second, third) = (context.ViewModel.QueryEditors[1], context.ViewModel.QueryEditors[2]);

        context.ViewModel.SelectedEditor = 0;
        Assert.Equal([first, third, second], context.ViewModel.EditorsByRecentUse);

        context.ViewModel.QueryEditors.Remove(third);
        Assert.Equal([first, second], context.ViewModel.EditorsByRecentUse);
    }

    [Fact]
    public void Switcher_QuickCtrlTab_GoesBackToThePreviousQuery()
    {
        // Like Rider/VS Code: from Query 1, click Query 3, then Ctrl+Tab returns to Query 1 and again to Query 3.
        using var context = CreateConnectionContext();
        context.ViewModel.OpenQueryEditorWithScript("select 2");
        context.ViewModel.OpenQueryEditorWithScript("select 3");
        var mainWindowViewModel = new MainWindowViewModel(new MainWindowServiceProviderStub());
        mainWindowViewModel.Connections.Add(context.ViewModel);
        context.ViewModel.SelectedEditor = 0;
        context.ViewModel.SelectedEditor = 2;

        mainWindowViewModel.ShowSwitcher(1);
        mainWindowViewModel.Switcher!.Commit();
        Assert.Equal(0, context.ViewModel.SelectedEditor);

        mainWindowViewModel.ShowSwitcher(1);
        mainWindowViewModel.Switcher!.Commit();
        Assert.Equal(2, context.ViewModel.SelectedEditor);
        Assert.Null(mainWindowViewModel.Switcher);
    }

    [Fact]
    public void Switcher_RepeatedCtrlTabWalksTheRecentList_AndShiftStartsFromTheOldest()
    {
        using var context = CreateConnectionContext();
        context.ViewModel.OpenQueryEditorWithScript("select 2");
        context.ViewModel.OpenQueryEditorWithScript("select 3");
        var mainWindowViewModel = new MainWindowViewModel(new MainWindowServiceProviderStub());
        mainWindowViewModel.Connections.Add(context.ViewModel);
        context.ViewModel.SelectedEditor = 0;
        context.ViewModel.SelectedEditor = 1;
        context.ViewModel.SelectedEditor = 2;
        var editors = context.ViewModel.QueryEditors;

        mainWindowViewModel.ShowSwitcher(1);
        mainWindowViewModel.ShowSwitcher(1);
        Assert.Same(editors[0], mainWindowViewModel.Switcher!.SelectedEditor);

        mainWindowViewModel.ShowSwitcher(1);
        Assert.Same(editors[2], mainWindowViewModel.Switcher!.SelectedEditor);

        mainWindowViewModel.Switcher.Cancel();
        Assert.Null(mainWindowViewModel.Switcher);
        Assert.Equal(2, context.ViewModel.SelectedEditor);

        mainWindowViewModel.ShowSwitcher(-1);
        Assert.Same(editors[0], mainWindowViewModel.Switcher!.SelectedEditor);
    }

    [Fact]
    public void Switcher_ConnectionColumn_SwitchesToTheHighlightedConnectionsRecentQuery()
    {
        using var first = CreateConnectionContext(Guid.NewGuid());
        using var second = CreateConnectionContext(Guid.NewGuid());
        second.ViewModel.OpenQueryEditorWithScript("select 2");
        second.ViewModel.SelectedEditor = 1;
        second.ViewModel.SelectedEditor = 0;
        var mainWindowViewModel = new MainWindowViewModel(new MainWindowServiceProviderStub());
        mainWindowViewModel.Connections.Add(first.ViewModel);
        mainWindowViewModel.Connections.Add(second.ViewModel);
        mainWindowViewModel.SelectedTabConnectionIndex = 0;

        mainWindowViewModel.ShowSwitcher(1);
        var switcher = mainWindowViewModel.Switcher!;
        switcher.ActivateConnectionColumn(true);
        switcher.Move(1);

        // The right column now lists the highlighted connection's queries, its current query first.
        Assert.Same(second.ViewModel, switcher.SelectedConnection);
        Assert.Same(second.ViewModel.QueryEditors[0], switcher.SelectedEditor);

        switcher.Commit();
        Assert.Equal(1, mainWindowViewModel.SelectedTabConnectionIndex);
        Assert.Equal(0, second.ViewModel.SelectedEditor);
    }

    [Fact]
    public void Switcher_ConnectionNumber_ListsThatConnectionsQueries_AndTabWalksThem()
    {
        using var first = CreateConnectionContext(Guid.NewGuid());
        using var second = CreateConnectionContext(Guid.NewGuid());
        second.ViewModel.OpenQueryEditorWithScript("select 2");
        var mainWindowViewModel = new MainWindowViewModel(new MainWindowServiceProviderStub());
        mainWindowViewModel.Connections.Add(first.ViewModel);
        mainWindowViewModel.Connections.Add(second.ViewModel);
        mainWindowViewModel.SelectedTabConnectionIndex = 0;

        mainWindowViewModel.ShowSwitcher(1);
        var switcher = mainWindowViewModel.Switcher!;
        Assert.Equal([1, 2], switcher.ConnectionItems.Select(item => item.Number));

        switcher.HighlightConnectionNumber(2);
        Assert.Same(second.ViewModel, switcher.SelectedConnection);
        Assert.False(switcher.IsConnectionColumnActive);

        // Ctrl+Tab again moves through the highlighted connection's queries.
        var current = switcher.SelectedEditor;
        mainWindowViewModel.ShowSwitcher(1);
        Assert.NotSame(current, switcher.SelectedEditor);
        Assert.Contains(switcher.SelectedEditor!, second.ViewModel.QueryEditors);

        switcher.HighlightConnectionNumber(3);
        Assert.Same(second.ViewModel, switcher.SelectedConnection);
    }

    [Fact]
    public void HasEditor_WhileTheSelectedEditorIsBeingRemoved_DoesNotThrow()
    {
        // Dock unloads a closed query's view in the middle of the removal, before SelectedEditor catches up, and the
        // view asks for the current editor; this used to crash the app when closing a connection.
        using var context = CreateConnectionContext();
        context.ViewModel.OpenQueryEditorWithScript("select 2");
        var mainWindowViewModel = new MainWindowViewModel(new MainWindowServiceProviderStub());
        mainWindowViewModel.Connections.Add(context.ViewModel);

        var hasEditorDuringRemoval = (bool?)null;
        context.ViewModel.QueryEditors.CollectionChanged += (_, _) => hasEditorDuringRemoval ??= mainWindowViewModel.HasEditor;

        context.ViewModel.QueryEditors.RemoveAt(context.ViewModel.SelectedEditor);

        Assert.NotNull(hasEditorDuringRemoval);
    }

    [Fact]
    public void HasCurrentFile_ReflectsWhetherTheSelectedTabHasAnExistingFileOnDisk()
    {
        using var context = CreateConnectionContext();
        var editor = context.ViewModel.QueryEditors[context.ViewModel.SelectedEditor];

        var mainWindowViewModel = new MainWindowViewModel(new MainWindowServiceProviderStub());
        mainWindowViewModel.Connections.Add(context.ViewModel);

        Assert.False(mainWindowViewModel.HasCurrentFile);

        var tempFile = Path.Combine(Path.GetTempPath(), $"datadeveloper-reveal-{Guid.NewGuid():N}.sql");
        try
        {
            File.WriteAllText(tempFile, "select 1");
            editor.File = tempFile;

            Assert.True(mainWindowViewModel.HasCurrentFile);

            editor.File = "/path/that/does/not/exist.sql";

            Assert.False(mainWindowViewModel.HasCurrentFile);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void AddRecentFile_MovesToFrontDeduplicatesAndCapsAtTwenty()
    {
        var mainWindowViewModel = new MainWindowViewModel(new MainWindowServiceProviderStub());
        var addRecentFile = typeof(MainWindowViewModel).GetMethod("AddRecentFile", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(addRecentFile);

        for (var i = 0; i < 25; i++)
            addRecentFile!.Invoke(mainWindowViewModel, new object[] { $"/tmp/recent-{i}.sql" });

        Assert.Equal(20, mainWindowViewModel.RecentFiles.Count);
        Assert.Equal("/tmp/recent-24.sql", mainWindowViewModel.RecentFiles[0]);
        Assert.DoesNotContain("/tmp/recent-4.sql", mainWindowViewModel.RecentFiles);
        Assert.True(mainWindowViewModel.HasRecentFiles);

        addRecentFile!.Invoke(mainWindowViewModel, new object[] { "/tmp/recent-10.sql" });

        Assert.Equal(20, mainWindowViewModel.RecentFiles.Count);
        Assert.Equal("/tmp/recent-10.sql", mainWindowViewModel.RecentFiles[0]);
    }

    [Fact]
    public void CanOpenRecentFiles_IsFalseWithoutAnOpenConnection_EvenWhenRecentFilesExist()
    {
        var serviceProvider = new MainWindowServiceProviderStub();
        serviceProvider.RecentFilesService.InitialFiles = new List<string> { "/tmp/a.sql" };
        var mainWindowViewModel = new MainWindowViewModel(serviceProvider);

        Assert.True(mainWindowViewModel.HasRecentFiles);
        Assert.False(mainWindowViewModel.CanOpenRecentFiles);

        using var context = CreateConnectionContext();
        mainWindowViewModel.Connections.Add(context.ViewModel);

        Assert.True(mainWindowViewModel.CanOpenRecentFiles);
    }

    [Fact]
    public async Task OpenRecentFileCommand_WithMissingFile_RemovesItAndShowsMessageInsteadOfThrowing()
    {
        using var context = CreateConnectionContext();
        var serviceProvider = new MainWindowServiceProviderStub();
        serviceProvider.RecentFilesService.InitialFiles = new List<string> { "/tmp/does-not-exist.sql" };
        var mainWindowViewModel = new MainWindowViewModel(serviceProvider);
        mainWindowViewModel.Connections.Add(context.ViewModel);

        Assert.True(mainWindowViewModel.HasRecentFiles);

        await mainWindowViewModel.OpenRecentFileCommand.Execute("/tmp/does-not-exist.sql").ToTask();

        Assert.False(mainWindowViewModel.HasRecentFiles);
        Assert.Empty(mainWindowViewModel.RecentFiles);
    }

    [Fact]
    public async Task ClearRecentFilesCommand_EmptiesTheList()
    {
        var serviceProvider = new MainWindowServiceProviderStub();
        serviceProvider.RecentFilesService.InitialFiles = new List<string> { "/tmp/a.sql", "/tmp/b.sql" };
        var mainWindowViewModel = new MainWindowViewModel(serviceProvider);

        Assert.True(mainWindowViewModel.HasRecentFiles);

        await mainWindowViewModel.ClearRecentFilesCommand.Execute().ToTask();

        Assert.False(mainWindowViewModel.HasRecentFiles);
        Assert.Empty(mainWindowViewModel.RecentFiles);
    }

    [Fact]
    public void OpenConnection_AlreadyOpen_SelectsItAndMovesItToTheTopOfRecentConnections()
    {
        using var first = CreateConnectionContext();
        using var second = CreateConnectionContext();
        var serviceProvider = new MainWindowServiceProviderStub();
        var olderId = Guid.NewGuid();
        serviceProvider.RecentConnectionsService.InitialConnectionIds = new List<Guid> { olderId, second.ConnectionSettings.Id };
        var mainWindowViewModel = new MainWindowViewModel(serviceProvider);
        mainWindowViewModel.Connections.Add(first.ViewModel);
        mainWindowViewModel.Connections.Add(second.ViewModel);
        mainWindowViewModel.SelectedTabConnectionIndex = 0;

        mainWindowViewModel.OpenConnection(second.ConnectionSettings);

        Assert.Equal(1, mainWindowViewModel.SelectedTabConnectionIndex);
        Assert.Equal(2, mainWindowViewModel.Connections.Count);
        Assert.Equal([second.ConnectionSettings.Id, olderId], mainWindowViewModel.RecentConnectionIds);
        Assert.Equal([second.ConnectionSettings.Id, olderId], serviceProvider.RecentConnectionsService.LastSaved);
    }

    [Fact]
    public void RecentConnectionIds_KeepAtMostTenConnections()
    {
        using var context = CreateConnectionContext();
        var serviceProvider = new MainWindowServiceProviderStub();
        serviceProvider.RecentConnectionsService.InitialConnectionIds = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid()).ToList();
        var mainWindowViewModel = new MainWindowViewModel(serviceProvider);
        Assert.Equal(10, mainWindowViewModel.RecentConnectionIds.Count);

        mainWindowViewModel.Connections.Add(context.ViewModel);
        mainWindowViewModel.OpenConnection(context.ConnectionSettings);

        Assert.Equal(10, mainWindowViewModel.RecentConnectionIds.Count);
        Assert.Equal(context.ConnectionSettings.Id, mainWindowViewModel.RecentConnectionIds[0]);
    }

    [Fact]
    public void ClearRecentConnections_EmptiesAndSavesTheList()
    {
        var serviceProvider = new MainWindowServiceProviderStub();
        serviceProvider.RecentConnectionsService.InitialConnectionIds = new List<Guid> { Guid.NewGuid() };
        var mainWindowViewModel = new MainWindowViewModel(serviceProvider);

        mainWindowViewModel.ClearRecentConnections();

        Assert.Empty(mainWindowViewModel.RecentConnectionIds);
        Assert.Empty(serviceProvider.RecentConnectionsService.LastSaved!);
    }

    [Fact]
    public void PersistSessionSnapshot_WritesEditorsWithContentToStore()
    {
        using var context = CreateConnectionContext();
        context.ViewModel.QueryEditors[0].SqlStatement = "select 1";
        context.ViewModel.OpenQueryEditorWithScript("select 2");

        context.ViewModel.PersistSessionSnapshot();

        var saved = context.SessionStore.Get(context.ConnectionSettings.Id);
        Assert.NotNull(saved);
        Assert.Equal(2, saved!.Editors.Count);
        Assert.Equal("select 1", saved.Editors[0].SqlStatement);
        Assert.Equal("select 2", saved.Editors[1].SqlStatement);
    }

    [Fact]
    public void PersistSessionSnapshot_SkipsEmptyUntouchedEditorsAndClearsStoreWhenNoneHaveContent()
    {
        using var context = CreateConnectionContext();
        context.ViewModel.OpenQueryEditorWithScript(string.Empty);
        context.SessionStore.Save(context.ConnectionSettings.Id, new List<EditorTabState> { new() { Name = "stale", SqlStatement = "old" } });

        context.ViewModel.PersistSessionSnapshot();

        Assert.Null(context.SessionStore.Get(context.ConnectionSettings.Id));
    }

    [Fact]
    public void PersistSessionSnapshot_SavesPanelLayouts_AndReopeningRestoresThem()
    {
        var sessionStore = new InMemorySessionTabStore();
        var connectionId = Guid.NewGuid();
        using (var context = CreateConnectionContext(connectionId, sessionStore))
        {
            context.ViewModel.QueryEditors[0].SqlStatement = "select 1";
            context.ViewModel.QueryEditors[0].ResultsLayout = new PanelLayoutState(0.6, true);
            context.ViewModel.SchemaExplorerLayout = new PanelLayoutState(0.3, false);

            context.ViewModel.PersistSessionSnapshot();
        }

        using var reopened = CreateConnectionContext(connectionId, sessionStore);

        Assert.Equal(new PanelLayoutState(0.3, false), reopened.ViewModel.SchemaExplorerLayout);
        Assert.Equal(new PanelLayoutState(0.6, true), Assert.Single(reopened.ViewModel.QueryEditors).ResultsLayout);
    }

    [Fact]
    public void PersistSessionSnapshot_KeepsSchemaExplorerLayout_WhenNoEditorHasContent()
    {
        using var context = CreateConnectionContext();
        context.ViewModel.SchemaExplorerLayout = new PanelLayoutState(null, true);

        context.ViewModel.PersistSessionSnapshot();

        var saved = context.SessionStore.Get(context.ConnectionSettings.Id);
        Assert.Equal(new PanelLayoutState(null, true), saved!.SchemaExplorer);
        Assert.Empty(saved.Editors);
    }

    [Fact]
    public void PersistSessionSnapshot_OmitsDefaultPanelLayouts()
    {
        using var context = CreateConnectionContext();
        context.ViewModel.QueryEditors[0].SqlStatement = "select 1";
        context.ViewModel.QueryEditors[0].ResultsLayout = new PanelLayoutState(null, false);
        context.ViewModel.SchemaExplorerLayout = new PanelLayoutState(null, false);

        context.ViewModel.PersistSessionSnapshot();

        var saved = context.SessionStore.Get(context.ConnectionSettings.Id);
        Assert.Null(saved!.SchemaExplorer);
        Assert.Null(Assert.Single(saved.Editors).Results);
    }

    [Fact]
    public void PersistSessionSnapshot_KeepsEditorWithContentButOmitsBlankSiblingTab()
    {
        using var context = CreateConnectionContext();
        context.ViewModel.QueryEditors[0].SqlStatement = "select 1";
        context.ViewModel.OpenQueryEditorWithScript(string.Empty);

        context.ViewModel.PersistSessionSnapshot();

        var saved = context.SessionStore.Get(context.ConnectionSettings.Id);
        Assert.NotNull(saved);
        var editor = Assert.Single(saved!.Editors);
        Assert.Equal("select 1", editor.SqlStatement);
    }

    [Fact]
    public void FindConnectionIndex_ReturnsIndexOfMatchingConnection_OrMinusOneWhenNotFound()
    {
        using var first = CreateConnectionContext();
        using var second = CreateConnectionContext();
        var connections = new List<TabConnectionViewModel> { first.ViewModel, second.ViewModel };

        Assert.Equal(0, MainWindowViewModel.FindConnectionIndex(connections, first.ConnectionSettings.Id));
        Assert.Equal(1, MainWindowViewModel.FindConnectionIndex(connections, second.ConnectionSettings.Id));
        Assert.Equal(-1, MainWindowViewModel.FindConnectionIndex(connections, Guid.NewGuid()));
    }

    private static (SessionTabStore Store, string Root) CreateFileBackedStore()
    {
        var fileService = new AppDataFileService();
        return (new SessionTabStore(fileService), AppDataFileService.AppDataDirectory);
    }

    [Fact]
    public async Task SchemaRefresh_MakesObjectsCreatedAfterTheFirstCompletionAvailable()
    {
        using var context = CreateConnectionContext();
        await context.ViewModel.Initialization;
        const string sql = "select * from ";

        // The first completion builds the connection's cache without the new table.
        Assert.DoesNotContain(await CompleteAsync(context.ConnectionSettings, sql), item => item.Text == "created_later");

        const string createStatement = "create table created_later (id integer primary key)";
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={context.ConnectionSettings.Database}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = createStatement;
            command.ExecuteNonQuery();
        }

        // What running DDL in the editor does: refresh the tab's schema tree.
        await context.ViewModel.SchemaExplorer.RefreshSchemaObjectAsync(createStatement);

        Assert.Contains(await CompleteAsync(context.ConnectionSettings, sql), item => item.Text == "created_later");
    }

    private static async Task<IReadOnlyList<AvaloniaEdit.CodeCompletion.ICompletionData>> CompleteAsync(Data.Interfaces.IConnectionSettings settings, string sql)
    {
        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);
        return await SqlCompletionProvider.GetCompletionsAsync(settings, sql, sql.Length, request);
    }

    [Fact]
    public async Task LoadSchemaNodeAsync_WhenTheLoadFails_ShowsTheErrorAndKeepsTheFolderRetryable()
    {
        using var context = CreateConnectionContext();
        await context.ViewModel.Initialization;
        var table = CreateSchemaNode(Data.Enums.NodeType.Table, "orders", isFolder: false, parent: null);
        var columns = CreateSchemaNode(Data.Enums.NodeType.Columns, "Columns", isFolder: true, parent: table);

        // A file that is not a SQLite database makes every catalog query fail.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        await File.WriteAllTextAsync(context.ConnectionSettings.Database, "this is not a database file, just some text padding it out");

        await context.ViewModel.LoadSchemaNodeAsync(columns);

        var (message, title) = Assert.Single(context.DialogService.Messages);
        Assert.Equal("Schema explorer", title);
        Assert.StartsWith("Could not load Columns of orders.", message, StringComparison.Ordinal);
        Assert.True(columns.CanLoad);
        Assert.Equal(Data.Enums.NodeType.None, Assert.Single(columns.Children).NodeType);
    }

    private static Data.Models.SchemaNode CreateSchemaNode(Data.Enums.NodeType nodeType, string name, bool isFolder, Data.Models.SchemaNode? parent)
    {
        return (Data.Models.SchemaNode)Activator.CreateInstance(
                   typeof(Data.Models.SchemaNode),
                   BindingFlags.Instance | BindingFlags.NonPublic,
                   binder: null,
                   args: [nodeType, name, isFolder, parent, isFolder, null, null],
                   culture: null)!;
    }

    private static ConnectionContext CreateConnectionContext(Guid? connectionId = null, ISessionTabStore? sessionStore = null)
    {
        EnsureDatabaseServices();

        var databasePath = Path.Combine(Path.GetTempPath(), $"datadeveloper-session-{Guid.NewGuid():N}.db");
        var connectionSettings = new SqLiteConnectionSettings
        {
            Id = connectionId ?? Guid.NewGuid(),
            Name = "Session test",
            DatabaseType = DatabaseType.SqLite,
            Database = databasePath
        };

        var dialogService = new RecordingDialogService();
        var store = sessionStore ?? new InMemorySessionTabStore();

        var services = new ServiceCollection();
        services.AddSingleton<IEventAggregatorService, EventAggregatorService>();
        services.AddSingleton<IDialogService>(dialogService);
        services.AddSingleton<ISessionTabStore>(store);
        services.AddSingleton<IFileImportDialogService>(new StubFileImportDialogService());
        var serviceProvider = services.BuildServiceProvider();

        var viewModel = new TabConnectionViewModel(connectionSettings, true, serviceProvider);

        return new ConnectionContext(viewModel, connectionSettings, dialogService, store, databasePath);
    }

    private static void EnsureDatabaseServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<DatabaseProviderFactoryService>();
        DatabaseExtensionsMethods.SetServiceProvider(services.BuildServiceProvider());
    }

    private sealed class ConnectionContext : IDisposable
    {
        public ConnectionContext(
            TabConnectionViewModel viewModel,
            SqLiteConnectionSettings connectionSettings,
            RecordingDialogService dialogService,
            ISessionTabStore sessionStore,
            string databasePath)
        {
            ViewModel = viewModel;
            ConnectionSettings = connectionSettings;
            DialogService = dialogService;
            SessionStore = sessionStore;
            _databasePath = databasePath;
        }

        private readonly string _databasePath;
        public TabConnectionViewModel ViewModel { get; }
        public SqLiteConnectionSettings ConnectionSettings { get; }
        public RecordingDialogService DialogService { get; }
        public ISessionTabStore SessionStore { get; }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(_databasePath))
                File.Delete(_databasePath);
        }
    }

    private sealed class InMemorySessionTabStore : ISessionTabStore
    {
        private readonly Dictionary<Guid, ConnectionSessionState> _states = new();

        public ConnectionSessionState? Get(Guid connectionId)
        {
            return _states.TryGetValue(connectionId, out var state) ? state : null;
        }

        public void Save(Guid connectionId, IReadOnlyList<EditorTabState> editors, PanelLayoutState? schemaExplorer = null)
        {
            _states[connectionId] = new ConnectionSessionState { ConnectionId = connectionId, Editors = new List<EditorTabState>(editors), SchemaExplorer = schemaExplorer };
        }

        public void Remove(Guid connectionId)
        {
            _states.Remove(connectionId);
        }
    }

    private sealed class RecordingDialogService : IDialogService
    {
        public int SaveChangesPromptCount { get; private set; }
        public int ShowSaveFileDialogCallCount { get; private set; }
        public DialogResult NextSaveChangesResult { get; set; } = DialogResult.No;

        public Task<DialogResult> ShowDialogAsync(string message, string? title = null, DialogButtons buttons = DialogButtons.Ok, DialogIcon icon = DialogIcon.Info)
        {
            return Task.FromResult(DialogResult.No);
        }

        public Task<DialogResult> ShowDialogResult(string message, string? title = null)
        {
            SaveChangesPromptCount++;
            return Task.FromResult(NextSaveChangesResult);
        }

        public List<(string Message, string? Title)> Messages { get; } = new();

        public Task ShowMessageAsync(string message, string? title = null)
        {
            Messages.Add((message, title));
            return Task.CompletedTask;
        }
        public Task ShowAboutAsync(string version, Func<Task> checkForUpdatesAsync) => Task.CompletedTask;
        public Task<DialogResult> ShowReleaseUpdateAsync(string message, string? title = null) => Task.FromResult(DialogResult.Cancel);

        public Task<string?> ShowSaveFileDialogAsync(string? suggestedName = null, string? title = null)
        {
            ShowSaveFileDialogCallCount++;
            return Task.FromResult<string?>(null);
        }

        public Task<string?> ShowOpenFileAsync(string? title = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenDatabaseFileAsync(string? title = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowCreateDatabaseFileAsync(string? suggestedName = null, string? title = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveJsonFileDialogAsync(string? suggestedName = null, string? title = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenJsonFileDialogAsync(string? title = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenImportFileAsync(string? title = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveExportFileDialogAsync(GridExportFormat format, string? suggestedName = null, string? title = null) => Task.FromResult<string?>(null);
    }

    private sealed class MainWindowServiceProviderStub : IServiceProvider
    {
        private readonly IEventAggregatorService _eventAggregatorService = new EventAggregatorService();

        public StubRecentFilesService RecentFilesService { get; } = new();
        public StubRecentConnectionsService RecentConnectionsService { get; } = new();

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IConnectionDialogService))
                return new StubConnectionDialogService();
            if (serviceType == typeof(IEventAggregatorService))
                return _eventAggregatorService;
            if (serviceType == typeof(IDialogService))
                return new RecordingDialogService();
            if (serviceType == typeof(IReleaseUpdateService))
                return new StubReleaseUpdateService();
            if (serviceType == typeof(IGenerateGuidWindowService))
                return new StubGenerateGuidWindowService();
            if (serviceType == typeof(IRecentFilesService))
                return RecentFilesService;
            if (serviceType == typeof(IRecentConnectionsService))
                return RecentConnectionsService;
            if (serviceType == typeof(ISchemaCompareDialogService))
                return new StubSchemaCompareDialogService();
            if (serviceType == typeof(IFileImportDialogService))
                return new StubFileImportDialogService();

            throw new NotSupportedException($"Service not configured for test: {serviceType}");
        }
    }

    private sealed class StubConnectionDialogService : IConnectionDialogService
    {
        public Task<IConnectionSettings?> ShowDialogAsync(Avalonia.Controls.Window parentWindow, DatabaseType? lockedDatabaseType = null) => Task.FromResult<IConnectionSettings?>(null);
    }

    private sealed class StubSchemaCompareDialogService : ISchemaCompareDialogService
    {
        public Task ShowDialogAsync(Avalonia.Controls.Window parentWindow) => Task.CompletedTask;
    }

    private sealed class StubFileImportDialogService : IFileImportDialogService
    {
        public Task<IConnectionSettings?> ShowDialogAsync(Avalonia.Controls.Window parentWindow, IConnectionSettings? preselectedConnection = null) =>
            Task.FromResult<IConnectionSettings?>(null);
    }

    private sealed class StubGenerateGuidWindowService : IGenerateGuidWindowService
    {
        public void Show(Avalonia.Controls.Window parentWindow)
        {
        }
    }

    private sealed class StubReleaseUpdateService : IReleaseUpdateService
    {
        public Task NotifyIfUpdateAvailableAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CheckForUpdatesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubRecentConnectionsService : IRecentConnectionsService
    {
        public List<Guid> InitialConnectionIds { get; set; } = new();
        public IReadOnlyList<Guid>? LastSaved { get; private set; }

        public IReadOnlyList<Guid> Load() => InitialConnectionIds;

        public void Save(IReadOnlyList<Guid> connectionIds)
        {
            LastSaved = connectionIds.ToList();
        }
    }

    private sealed class StubRecentFilesService : IRecentFilesService
    {
        public List<string> InitialFiles { get; set; } = new();
        public IReadOnlyList<string>? LastSaved { get; private set; }

        public IReadOnlyList<string> Load() => InitialFiles;

        public void Save(IReadOnlyList<string> files)
        {
            LastSaved = files.ToList();
        }
    }
}
