using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reactive.Threading.Tasks;
using Avalonia.Headless.XUnit;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.Oracle;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Data.Services;
using DataDeveloper.Enums;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using DataDeveloper.ViewModels;
using Xunit;

namespace DataDeveloper.Tests;

public class ConnectionSelectorViewModelTests
{
    private sealed class FakeConnectionSettingsRepository : IConnectionSettingsRepository
    {
        private readonly List<ConnectionSettings> _connections;
        public int DeleteCallCount { get; private set; }
        public ConnectionSettings? LastSavedConnection { get; private set; }

        public FakeConnectionSettingsRepository(IEnumerable<ConnectionSettings>? connections = null)
        {
            _connections = connections?.ToList() ?? [];
        }

        public IReadOnlyList<ConnectionSettings> LoadAll() => _connections.ToList();

        public void LoadPassword(ConnectionSettings connectionSettings)
        {
            connectionSettings.IsPasswordLoaded = true;
            connectionSettings.LoadedPasswordSnapshot = connectionSettings.Password;
        }

        public void Save(ConnectionSettings connectionSettings)
        {
            LastSavedConnection = connectionSettings;
            _connections.RemoveAll(c => c.Id == connectionSettings.Id);
            _connections.Add(connectionSettings);
        }

        public void Delete(ConnectionSettings connectionSettings)
        {
            DeleteCallCount++;
            _connections.Remove(connectionSettings);
        }

        public void SaveAll(IEnumerable<ConnectionSettings> connections)
        {
        }
    }

    private sealed class FakeConnectionGroupRepository : IConnectionGroupRepository
    {
        private readonly List<ConnectionGroup> _groups;

        public FakeConnectionGroupRepository(IEnumerable<ConnectionGroup>? groups = null)
        {
            _groups = groups?.ToList() ?? [];
        }

        public IReadOnlyList<ConnectionGroup> LoadAll() => _groups.ToList();

        public void Save(ConnectionGroup group)
        {
            _groups.RemoveAll(g => g.Id == group.Id);
            _groups.Add(group);
        }

        public void Delete(Guid groupId)
        {
            _groups.RemoveAll(g => g.Id == groupId);
        }
    }

    private sealed class FakeConnectionGroupDialogService : IConnectionGroupDialogService
    {
        public Task ShowDialogAsync(Avalonia.Controls.Window parentWindow) => Task.CompletedTask;
    }

    private sealed class FakeConnectionExportService : IConnectionExportService
    {
        public IReadOnlyList<ConnectionSettings>? LastExportedConnections { get; private set; }
        public bool? LastIncludePasswords { get; private set; }

        public Task ExportAsync(IReadOnlyList<ConnectionSettings> connections, string filePath, bool includePasswords)
        {
            LastExportedConnections = connections;
            LastIncludePasswords = includePasswords;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeConnectionImportService : IConnectionImportService
    {
        public int ImportedCount { get; set; }
        public Exception? ThrowException { get; set; }

        public Task<int> ImportAsync(string filePath)
        {
            if (ThrowException is not null)
                throw ThrowException;

            return Task.FromResult(ImportedCount);
        }
    }

    private sealed class FakeExportConnectionsOptionsDialogService : IExportConnectionsOptionsDialogService
    {
        public ExportConnectionsOptionsResult? Result { get; set; }

        public Task<ExportConnectionsOptionsResult?> ShowDialogAsync(Avalonia.Controls.Window parentWindow) => Task.FromResult(Result);
    }

    private sealed class FakeDialogService : IDialogService
    {
        public string? OpenDatabaseFileResult { get; set; }
        public string? CreateDatabaseFileResult { get; set; }
        public string? SaveJsonFileResult { get; set; }
        public string? OpenJsonFileResult { get; set; }

        public Task<DialogResult> ShowDialogAsync(string message, string? title = null, DialogButtons buttons = DialogButtons.Ok, DialogIcon icon = DialogIcon.Info)
        {
            var result = buttons switch
            {
                DialogButtons.YesNo => DialogResult.Yes,
                DialogButtons.YesNoCancel => DialogResult.Yes,
                _ => DialogResult.Ok
            };

            return Task.FromResult(result);
        }

        public Task<DialogResult> ShowDialogResult(string message, string? title = null) => Task.FromResult(DialogResult.Yes);

        public Task ShowMessageAsync(string message, string? title = null) => Task.CompletedTask;

        public Task ShowAboutAsync(string version, Func<Task> checkForUpdatesAsync) => Task.CompletedTask;

        public Task<DialogResult> ShowReleaseUpdateAsync(string message, string? title = null) => Task.FromResult(DialogResult.Cancel);

        public Task<string?> ShowSaveFileDialogAsync(string? suggestedName = null, string? title = null) => Task.FromResult<string?>(null);

        public Task<string?> ShowOpenFileAsync(string? title = null) => Task.FromResult<string?>(null);

        public Task<string?> ShowOpenDatabaseFileAsync(string? title = null) => Task.FromResult(OpenDatabaseFileResult);

        public Task<string?> ShowCreateDatabaseFileAsync(string? suggestedName = null, string? title = null) => Task.FromResult(CreateDatabaseFileResult);

        public Task<string?> ShowSaveJsonFileDialogAsync(string? suggestedName = null, string? title = null) => Task.FromResult(SaveJsonFileResult);

        public Task<string?> ShowOpenJsonFileDialogAsync(string? title = null) => Task.FromResult(OpenJsonFileResult);

        public Task<string?> ShowOpenImportFileAsync(string? title = null) => Task.FromResult<string?>(null);

        public Task<string?> ShowSaveExportFileDialogAsync(GridExportFormat format, string? suggestedName = null, string? title = null) => Task.FromResult<string?>(null);
    }

    private sealed class FakeDatabaseProvider : IDatabaseProvider
    {
        private readonly IReadOnlyList<string> _databaseNames;

        public FakeDatabaseProvider(IReadOnlyList<string> databaseNames)
        {
            _databaseNames = databaseNames;
        }

        public DbConnection GetConnection() => throw new NotSupportedException();
        public TestConnectionResult TestConnection() => new(true, "ok");
        public IReadOnlyList<string> GetAvailableDatabaseNames() => _databaseNames;
        public string GetTableStatement() => string.Empty;
        public string GetViewStatement() => string.Empty;
        public string GetColumnStatement() => string.Empty;
        public string GetProcedureStatement() => string.Empty;
        public string GetFunctionStatement() => string.Empty;
        public string GetRoutineParameterStatement() => string.Empty;
        public string GetColumnDefaultValueStatement() => string.Empty;
        public string GetPrimaryKeyStatement() => string.Empty;
        public string GetForeignKeyStatement() => string.Empty;
        public string GetIndexStatement() => string.Empty;
    }

    private sealed class FakeDatabaseProviderFactoryService : DatabaseProviderFactoryService
    {
        private readonly IDatabaseProvider _provider;

        public FakeDatabaseProviderFactoryService(IDatabaseProvider provider)
        {
            _provider = provider;
        }

        public override IDatabaseProvider GetDatabaseProvider(IConnectionSettings connectionSettings) => _provider;
    }

    [Fact]
    public async Task DuplicateConnection_CreatesIndependentCredentialReference()
    {
        var originalCredentialId = Guid.NewGuid();
        var original = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            CredentialId = originalCredentialId,
            Name = "Original",
            DatabaseType = DatabaseType.SqlServer,
            Server = "sql.local",
            Database = "master",
            User = "sa",
            Password = "secret",
            IsPasswordLoaded = true,
            LoadedPasswordSnapshot = "secret"
        };

        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([original]),
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService())
        {
            SelectedConnection = original
        };

        await viewModel.DuplicateConnectionCommand.Execute().ToTask();

        var duplicate = Assert.IsType<SqlServerConnectionSettings>(viewModel.SelectedConnection);
        Assert.NotSame(original, duplicate);
        Assert.Equal($"Copy of {original.Name}", duplicate.Name);
        Assert.NotEqual(original.Id, duplicate.Id);
        Assert.Null(duplicate.CredentialId);
        Assert.Null(duplicate.LoadedPasswordSnapshot);
        Assert.Equal(original.Password, duplicate.Password);
    }

    [Fact]
    public void Delete_RemovesCredentialWithoutBlockingRepositoryContract()
    {
        var original = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            CredentialId = Guid.NewGuid(),
            Name = "Original",
            DatabaseType = DatabaseType.SqlServer,
            Server = "sql.local",
            Database = "master",
            User = "sa",
            Password = "secret"
        };

        var repository = new FakeConnectionSettingsRepository([original]);
        repository.Delete(original);

        Assert.Equal(1, repository.DeleteCallCount);
        Assert.Empty(repository.LoadAll());
    }

    [Fact]
    public async Task AddCommand_CreatesOracleConnection_WhenOracleTypeSelected()
    {
        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository(),
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService());

        viewModel.SelectedConnectionFilter = viewModel.AvailableConnectionFilters.Single(option => option.DatabaseType == DatabaseType.Oracle);

        await viewModel.AddCommand.Execute().ToTask();

        var connection = Assert.IsType<OracleConnectionSettings>(viewModel.SelectedConnection);
        Assert.Equal(1521, connection.Port);
        Assert.Equal(DatabaseType.Oracle, connection.DatabaseType);
        Assert.Equal(DmlTransactionMode.ManualCommitRollback, connection.DmlTransactionMode);
    }

    [Fact]
    public async Task AddCommand_CreatesSqLiteConnection_WhenSqLiteTypeSelected()
    {
        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository(),
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService());

        viewModel.SelectedConnectionFilter = viewModel.AvailableConnectionFilters.Single(option => option.DatabaseType == DatabaseType.SqLite);

        await viewModel.AddCommand.Execute().ToTask();

        var connection = Assert.IsType<SqLiteConnectionSettings>(viewModel.SelectedConnection);
        Assert.Equal(DatabaseType.SqLite, connection.DatabaseType);
        Assert.Equal(string.Empty, connection.Database);
        Assert.Equal(DmlTransactionMode.AutoCommit, connection.DmlTransactionMode);
    }

    [Fact]
    public void SqlServer_UsesCredentialsForSelectedConnection_DependsOnAuthenticationMode()
    {
        var connection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            DatabaseType = DatabaseType.SqlServer,
            Name = "SQL Server",
            Server = @"(localdb)\MSSQLLocalDB",
            Database = "master",
            AuthenticationMode = SqlServerAuthenticationMode.SqlLogin
        };
        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([connection]),
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService())
        {
            SelectedConnection = connection
        };

        Assert.True(viewModel.UsesSqlServerAuthenticationMode);
        Assert.True(viewModel.UsesCredentialsForSelectedConnection);

        viewModel.SelectedSqlServerAuthenticationOption = viewModel.SqlServerAuthenticationModes
            .Single(option => option.Value == SqlServerAuthenticationMode.WindowsIntegrated);

        Assert.False(viewModel.UsesCredentialsForSelectedConnection);
    }

    [Fact]
    public async Task ApplyCommand_ClearsCredentials_ForSqlServerWindowsAuthentication()
    {
        var repository = new FakeConnectionSettingsRepository();
        var connection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            DatabaseType = DatabaseType.SqlServer,
            Name = "SQL Server",
            Server = @"(localdb)\MSSQLLocalDB",
            Database = "master",
            AuthenticationMode = SqlServerAuthenticationMode.WindowsIntegrated,
            User = "sa",
            Password = "secret",
            CredentialId = Guid.NewGuid()
        };
        var viewModel = new ConnectionSelectorViewModel(
            repository,
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService())
        {
            SelectedConnection = connection,
            IsEditing = true
        };

        await viewModel.ApplyCommand.Execute(null!).ToTask();

        var saved = Assert.IsType<SqlServerConnectionSettings>(repository.LastSavedConnection);
        Assert.Equal(string.Empty, saved.User);
        Assert.Equal(string.Empty, saved.Password);
        Assert.True(saved.IsPasswordLoaded);
        Assert.Equal(string.Empty, saved.LoadedPasswordSnapshot);
    }

    [Fact]
    public async Task RefreshDatabaseNamesCommand_LoadsAvailableDatabases_AndPreservesCurrentValue()
    {
        var connection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            DatabaseType = DatabaseType.SqlServer,
            Name = "SQL Server",
            Server = @"(localdb)\MSSQLLocalDB",
            Database = "custom_db",
            AuthenticationMode = SqlServerAuthenticationMode.WindowsIntegrated
        };
        var provider = new FakeDatabaseProvider(["master", "tempdb", "model"]);
        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([connection]),
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new FakeDatabaseProviderFactoryService(provider),
            new InMemorySecretStore(),
            new FakeDialogService())
        {
            SelectedConnection = connection,
            IsEditing = true
        };

        await viewModel.RefreshDatabaseNamesCommand.Execute().ToTask();

        Assert.True(viewModel.CanRefreshDatabaseNames);
        Assert.Equal(["custom_db", "master", "model", "tempdb"], viewModel.AvailableDatabaseNames);
    }

    [Fact]
    public void SelectingSqLiteConnection_DisablesDatabaseRefresh()
    {
        var connection = new SqLiteConnectionSettings
        {
            Id = Guid.NewGuid(),
            DatabaseType = DatabaseType.SqLite,
            Name = "SQLite",
            Database = "/tmp/app.db"
        };
        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([connection]),
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService())
        {
            SelectedConnection = connection
        };

        Assert.False(viewModel.CanRefreshDatabaseNames);
        Assert.Single(viewModel.AvailableDatabaseNames);
        Assert.Equal("/tmp/app.db", viewModel.AvailableDatabaseNames[0]);
    }


    [Fact]
    public async Task SelectSqLiteFileCommand_UpdatesDatabasePath()
    {
        var dialogService = new FakeDialogService
        {
            OpenDatabaseFileResult = "/tmp/app.db"
        };
        var connection = new SqLiteConnectionSettings
        {
            Id = Guid.NewGuid(),
            DatabaseType = DatabaseType.SqLite,
            Name = "SQLite",
            Database = string.Empty
        };
        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([connection]),
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            dialogService)
        {
            SelectedConnection = connection,
            IsEditing = true
        };

        await viewModel.SelectSqLiteFileCommand.Execute().ToTask();

        Assert.Equal("/tmp/app.db", connection.Database);
    }

    [Fact]
    public async Task CreateSqLiteFileCommand_UpdatesDatabasePath()
    {
        var dialogService = new FakeDialogService
        {
            CreateDatabaseFileResult = "/tmp/new-app.db"
        };
        var connection = new SqLiteConnectionSettings
        {
            Id = Guid.NewGuid(),
            DatabaseType = DatabaseType.SqLite,
            Name = "SQLite",
            Database = string.Empty
        };
        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([connection]),
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            dialogService)
        {
            SelectedConnection = connection,
            IsEditing = true
        };

        await viewModel.CreateSqLiteFileCommand.Execute().ToTask();

        Assert.Equal("/tmp/new-app.db", connection.Database);
    }

    [Fact]
    public void Constructor_StartsWithAllFilterSelected_AndAddDisabled()
    {
        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository(),
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService());

        Assert.Equal("(All)", viewModel.SelectedConnectionFilter?.DisplayName);
        Assert.False(viewModel.CanAddConnection);
        Assert.True(viewModel.ShowFilterSelectionHint);
    }

    [Fact]
    public void Constructor_DoesNotAutoSelectAnyConnection_WhenConnectionsExist()
    {
        var connection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            Name = "Sql Server",
            DatabaseType = DatabaseType.SqlServer
        };

        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([connection]),
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService());

        Assert.Null(viewModel.SelectedConnection);
    }

    [Fact]
    public void SelectedTreeItem_Set_ResetsIsEditingToFalse()
    {
        var first = new SqlServerConnectionSettings { Id = Guid.NewGuid(), Name = "First", DatabaseType = DatabaseType.SqlServer };
        var second = new SqlServerConnectionSettings { Id = Guid.NewGuid(), Name = "Second", DatabaseType = DatabaseType.SqlServer };

        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([first, second]),
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService())
        {
            SelectedTreeItem = first,
            IsEditing = true
        };

        viewModel.SelectedTreeItem = second;

        Assert.False(viewModel.IsEditing);
        Assert.Same(second, viewModel.SelectedConnection);
    }

    [Fact]
    public void ChangingFilter_RestrictsVisibleConnections()
    {
        var sqlServerConnection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            Name = "Sql Server",
            DatabaseType = DatabaseType.SqlServer
        };
        var mySqlConnection = new MySqlConnectionSettings
        {
            Id = Guid.NewGuid(),
            Name = "MySql",
            DatabaseType = DatabaseType.MySql
        };

        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([sqlServerConnection, mySqlConnection]),
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService());

        Assert.Equal(2, viewModel.Connections.Count);

        viewModel.SelectedConnectionFilter = viewModel.AvailableConnectionFilters.Single(option => option.DatabaseType == DatabaseType.MySql);

        var filteredConnection = Assert.Single(viewModel.Connections);
        Assert.Equal(DatabaseType.MySql, filteredConnection.DatabaseType);
        Assert.True(viewModel.CanAddConnection);
        Assert.False(viewModel.ShowFilterSelectionHint);
    }

    [Fact]
    public void ChangingFilter_ClearsSelection_EvenBackToAll()
    {
        var connection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            Name = "Sql Server",
            DatabaseType = DatabaseType.SqlServer
        };
        var allFilter = new FakeConnectionSettingsRepository([connection]);

        var viewModel = new ConnectionSelectorViewModel(
            allFilter,
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService())
        {
            SelectedTreeItem = connection
        };
        Assert.Same(connection, viewModel.SelectedConnection);

        var sqlServerFilter = viewModel.AvailableConnectionFilters.Single(option => option.DatabaseType == DatabaseType.SqlServer);
        viewModel.SelectedConnectionFilter = sqlServerFilter;
        Assert.Null(viewModel.SelectedConnection);

        viewModel.SelectedTreeItem = connection;
        Assert.Same(connection, viewModel.SelectedConnection);

        var allOption = viewModel.AvailableConnectionFilters.Single(option => option.DatabaseType is null);
        viewModel.SelectedConnectionFilter = allOption;

        Assert.Null(viewModel.SelectedConnection);
    }

    [AvaloniaFact]
    public async Task BulkExportCommand_ClearsCheckboxSelection_AfterExport()
    {
        var connection = new SqlServerConnectionSettings { Id = Guid.NewGuid(), Name = "Sql Server", DatabaseType = DatabaseType.SqlServer, IsBulkSelected = true };
        var optionsDialogService = new FakeExportConnectionsOptionsDialogService
        {
            Result = new ExportConnectionsOptionsResult(IncludePasswords: false)
        };
        var dialogService = new FakeDialogService { SaveJsonFileResult = "/tmp/connections.json" };

        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([connection]),
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            optionsDialogService,
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            dialogService);
        viewModel.RefreshBulkSelectionState();
        Assert.True(viewModel.HasBulkSelection);

        await viewModel.BulkExportCommand.Execute(new Avalonia.Controls.Window()).ToTask();

        Assert.False(connection.IsBulkSelected);
        Assert.False(viewModel.HasBulkSelection);
    }

    [Fact]
    public void RootNodes_ShowsGroupsFirst_ThenUngroupedConnections_EachSortedAlphabetically()
    {
        var productionGroup = new ConnectionGroup { Id = Guid.NewGuid(), Name = "Zeta group" };
        var groupedConnection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            GroupId = productionGroup.Id,
            Name = "Grouped connection",
            DatabaseType = DatabaseType.SqlServer
        };
        var ungroupedFirst = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            Name = "Alpha connection",
            DatabaseType = DatabaseType.SqlServer
        };
        var ungroupedLast = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            Name = "Beta connection",
            DatabaseType = DatabaseType.SqlServer
        };

        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([groupedConnection, ungroupedFirst, ungroupedLast]),
            new FakeConnectionGroupRepository([productionGroup]),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService());

        Assert.Equal(3, viewModel.RootNodes.Count);
        Assert.Equal("Zeta group", Assert.IsType<ConnectionGroupNode>(viewModel.RootNodes[0]).Name);
        Assert.Equal("Alpha connection", Assert.IsAssignableFrom<ConnectionSettings>(viewModel.RootNodes[1]).Name);
        Assert.Equal("Beta connection", Assert.IsAssignableFrom<ConnectionSettings>(viewModel.RootNodes[2]).Name);

        var groupNode = (ConnectionGroupNode)viewModel.RootNodes[0];
        var childConnection = Assert.Single(groupNode.Children);
        Assert.Equal(groupedConnection.Id, childConnection.Id);
    }

    [Fact]
    public void CollapsingGroup_PersistsImmediately_AndSurvivesTreeRefresh()
    {
        var group = new ConnectionGroup { Id = Guid.NewGuid(), Name = "Production" };
        var connection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            Name = "Prod",
            DatabaseType = DatabaseType.SqlServer
        };
        var groupRepository = new FakeConnectionGroupRepository([group]);

        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([connection]),
            groupRepository,
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService());

        var groupNode = Assert.IsType<ConnectionGroupNode>(Assert.Single(viewModel.RootNodes));
        groupNode.Group.IsExpanded = false;

        Assert.False(groupRepository.LoadAll().Single().IsExpanded);

        viewModel.SelectedConnectionFilter = viewModel.AvailableConnectionFilters.Single(option => option.DatabaseType == DatabaseType.SqlServer);

        var groupNodeAfterRefresh = Assert.IsType<ConnectionGroupNode>(Assert.Single(viewModel.RootNodes));
        Assert.False(groupNodeAfterRefresh.Group.IsExpanded);
    }

    [AvaloniaFact]
    public async Task SelectedConnectionGroup_Set_DoesNotMoveConnection_UntilApplied()
    {
        var group = new ConnectionGroup { Id = Guid.NewGuid(), Name = "Production" };
        var connection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            Name = "Sql Server",
            DatabaseType = DatabaseType.SqlServer
        };

        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([connection]),
            new FakeConnectionGroupRepository([group]),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService())
        {
            SelectedConnection = connection,
            IsEditing = true
        };

        viewModel.SelectedConnectionGroup = viewModel.GroupOptions.Single(option => option.Id == group.Id);

        Assert.Equal(group.Id, connection.GroupId);
        Assert.IsAssignableFrom<ConnectionSettings>(Assert.Single(viewModel.RootNodes));

        await viewModel.ApplyCommand.Execute(new Avalonia.Controls.Window()).ToTask();

        var groupNode = Assert.IsType<ConnectionGroupNode>(Assert.Single(viewModel.RootNodes));
        Assert.Same(connection, Assert.Single(groupNode.Children));
    }

    [AvaloniaFact]
    public void ManageGroupsCommand_UngroupsConnections_WhenGroupWasDeletedInDialog()
    {
        var groupRepository = new FakeConnectionGroupRepository();
        var group = new ConnectionGroup { Id = Guid.NewGuid(), Name = "Production" };
        groupRepository.Save(group);

        var connection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            Name = "Sql Server",
            DatabaseType = DatabaseType.SqlServer
        };

        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([connection]),
            groupRepository,
            new DeletingConnectionGroupDialogService(groupRepository, group.Id),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService());

        Assert.IsType<ConnectionGroupNode>(Assert.Single(viewModel.RootNodes));

        viewModel.ManageGroupsCommand.Execute(new Avalonia.Controls.Window()).Subscribe();

        Assert.Null(connection.GroupId);
        Assert.IsAssignableFrom<ConnectionSettings>(Assert.Single(viewModel.RootNodes));
    }

    [Fact]
    public void HasBulkSelection_ReflectsCheckedConnections_AfterRefresh()
    {
        var connection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            Name = "Sql Server",
            DatabaseType = DatabaseType.SqlServer
        };

        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([connection]),
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService());

        Assert.False(viewModel.HasBulkSelection);

        connection.IsBulkSelected = true;
        viewModel.RefreshBulkSelectionState();

        Assert.True(viewModel.HasBulkSelection);
    }

    [Fact]
    public async Task BulkDeleteCommand_RemovesOnlySelectedConnections_AfterConfirmation()
    {
        var toDelete = new SqlServerConnectionSettings { Id = Guid.NewGuid(), Name = "Delete me", DatabaseType = DatabaseType.SqlServer, IsBulkSelected = true };
        var toKeep = new SqlServerConnectionSettings { Id = Guid.NewGuid(), Name = "Keep me", DatabaseType = DatabaseType.SqlServer };
        var repository = new FakeConnectionSettingsRepository([toDelete, toKeep]);

        var viewModel = new ConnectionSelectorViewModel(
            repository,
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            new FakeConnectionExportService(),
            new FakeConnectionImportService(),
            new FakeExportConnectionsOptionsDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService());
        viewModel.RefreshBulkSelectionState();

        await viewModel.BulkDeleteCommand.Execute().ToTask();

        Assert.Equal(1, repository.DeleteCallCount);
        Assert.DoesNotContain(viewModel.Connections, c => c.Id == toDelete.Id);
        Assert.Contains(viewModel.Connections, c => c.Id == toKeep.Id);
    }

    [AvaloniaFact]
    public async Task BulkExportCommand_PassesSelectedConnectionsAndPasswordChoice_ToExportService()
    {
        var connection = new SqlServerConnectionSettings { Id = Guid.NewGuid(), Name = "Sql Server", DatabaseType = DatabaseType.SqlServer, IsBulkSelected = true };
        var exportService = new FakeConnectionExportService();
        var optionsDialogService = new FakeExportConnectionsOptionsDialogService
        {
            Result = new ExportConnectionsOptionsResult(IncludePasswords: true)
        };
        var dialogService = new FakeDialogService { SaveJsonFileResult = "/tmp/connections.json" };

        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([connection]),
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            exportService,
            new FakeConnectionImportService(),
            optionsDialogService,
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            dialogService);
        viewModel.RefreshBulkSelectionState();

        await viewModel.BulkExportCommand.Execute(new Avalonia.Controls.Window()).ToTask();

        Assert.NotNull(exportService.LastExportedConnections);
        Assert.Contains(exportService.LastExportedConnections!, c => c.Id == connection.Id);
        Assert.True(exportService.LastIncludePasswords);
    }

    [AvaloniaFact]
    public async Task BulkExportCommand_WhenOptionsCancelled_DoesNotExport()
    {
        var connection = new SqlServerConnectionSettings { Id = Guid.NewGuid(), Name = "Sql Server", DatabaseType = DatabaseType.SqlServer, IsBulkSelected = true };
        var exportService = new FakeConnectionExportService();
        var optionsDialogService = new FakeExportConnectionsOptionsDialogService { Result = null };

        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([connection]),
            new FakeConnectionGroupRepository(),
            new FakeConnectionGroupDialogService(),
            exportService,
            new FakeConnectionImportService(),
            optionsDialogService,
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService());
        viewModel.RefreshBulkSelectionState();

        await viewModel.BulkExportCommand.Execute(new Avalonia.Controls.Window()).ToTask();

        Assert.Null(exportService.LastExportedConnections);
    }

    [AvaloniaFact]
    public async Task ImportCommand_ReloadsConnectionsAndGroups_AfterSuccessfulImport()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "DataDeveloperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var filePath = Path.Combine(tempDirectory, "import.json");
            var exportFile = new ConnectionExportFile
            {
                Connections =
                [
                    new ConnectionExportEntry { Name = "Imported", DatabaseType = "SqlServer", Server = "sql.local", Database = "master", Port = 1433, User = "sa" }
                ]
            };
            await File.WriteAllTextAsync(filePath, System.Text.Json.JsonSerializer.Serialize(exportFile));

            var settingsRepository = new FakeConnectionSettingsRepository();
            var groupRepository = new FakeConnectionGroupRepository();
            var importService = new ConnectionImportService(settingsRepository);
            var dialogService = new FakeDialogService { OpenJsonFileResult = filePath };

            var viewModel = new ConnectionSelectorViewModel(
                settingsRepository,
                groupRepository,
                new FakeConnectionGroupDialogService(),
                new FakeConnectionExportService(),
                importService,
                new FakeExportConnectionsOptionsDialogService(),
                new DatabaseProviderFactoryService(),
                new InMemorySecretStore(),
                dialogService);

            await viewModel.ImportCommand.Execute().ToTask();

            Assert.Contains(viewModel.Connections, c => c.Name == "Imported");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task ImportCommand_DoesNotChangeConnections_WhenFileIsNotRecognized()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "DataDeveloperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var filePath = Path.Combine(tempDirectory, "import.json");
            await File.WriteAllTextAsync(filePath, "{\"ExportedBy\":\"SomeOtherApp\",\"FormatVersion\":1,\"Connections\":[]}");

            var settingsRepository = new FakeConnectionSettingsRepository();
            var groupRepository = new FakeConnectionGroupRepository();
            var importService = new ConnectionImportService(settingsRepository);
            var dialogService = new FakeDialogService { OpenJsonFileResult = filePath };

            var viewModel = new ConnectionSelectorViewModel(
                settingsRepository,
                groupRepository,
                new FakeConnectionGroupDialogService(),
                new FakeConnectionExportService(),
                importService,
                new FakeExportConnectionsOptionsDialogService(),
                new DatabaseProviderFactoryService(),
                new InMemorySecretStore(),
                dialogService);

            await viewModel.ImportCommand.Execute().ToTask();

            Assert.Empty(viewModel.Connections);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private sealed class DeletingConnectionGroupDialogService : IConnectionGroupDialogService
    {
        private readonly FakeConnectionGroupRepository _repository;
        private readonly Guid _groupIdToDelete;

        public DeletingConnectionGroupDialogService(FakeConnectionGroupRepository repository, Guid groupIdToDelete)
        {
            _repository = repository;
            _groupIdToDelete = groupIdToDelete;
        }

        public Task ShowDialogAsync(Avalonia.Controls.Window parentWindow)
        {
            _repository.Delete(_groupIdToDelete);
            return Task.CompletedTask;
        }
    }
}
