using System;
using System.Collections.Generic;
using System.IO;
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

        public void Save(Guid connectionId, IReadOnlyList<EditorTabState> editors)
        {
            _states[connectionId] = new ConnectionSessionState { ConnectionId = connectionId, Editors = new List<EditorTabState>(editors) };
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

        public Task ShowMessageAsync(string message, string? title = null) => Task.CompletedTask;
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
    }

    private sealed class MainWindowServiceProviderStub : IServiceProvider
    {
        private readonly IEventAggregatorService _eventAggregatorService = new EventAggregatorService();

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

            throw new NotSupportedException($"Service not configured for test: {serviceType}");
        }
    }

    private sealed class StubConnectionDialogService : IConnectionDialogService
    {
        public Task<IConnectionSettings?> ShowDialogAsync(Avalonia.Controls.Window parentWindow) => Task.FromResult<IConnectionSettings?>(null);
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
}
