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

    private sealed class FakeDialogService : IDialogService
    {
        public string? OpenDatabaseFileResult { get; set; }
        public string? CreateDatabaseFileResult { get; set; }

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
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService());

        Assert.Equal("(All)", viewModel.SelectedConnectionFilter?.DisplayName);
        Assert.False(viewModel.CanAddConnection);
        Assert.True(viewModel.ShowFilterSelectionHint);
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
    public void RootNodes_MixesGroupsAndUngroupedConnections_SortedAlphabeticallyByName()
    {
        var productionGroup = new ConnectionGroup { Id = Guid.NewGuid(), Name = "Beta group" };
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
            Name = "Zeta connection",
            DatabaseType = DatabaseType.SqlServer
        };

        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository([groupedConnection, ungroupedFirst, ungroupedLast]),
            new FakeConnectionGroupRepository([productionGroup]),
            new FakeConnectionGroupDialogService(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService());

        Assert.Equal(3, viewModel.RootNodes.Count);
        Assert.Equal("Alpha connection", Assert.IsAssignableFrom<ConnectionSettings>(viewModel.RootNodes[0]).Name);
        Assert.Equal("Beta group", Assert.IsType<ConnectionGroupNode>(viewModel.RootNodes[1]).Name);
        Assert.Equal("Zeta connection", Assert.IsAssignableFrom<ConnectionSettings>(viewModel.RootNodes[2]).Name);

        var groupNode = (ConnectionGroupNode)viewModel.RootNodes[1];
        var childConnection = Assert.Single(groupNode.Children);
        Assert.Equal(groupedConnection.Id, childConnection.Id);
    }

    [Fact]
    public void SelectedConnectionGroup_Set_MovesConnectionIntoGroupNode()
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
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService())
        {
            SelectedConnection = connection
        };

        Assert.IsAssignableFrom<ConnectionSettings>(Assert.Single(viewModel.RootNodes));

        viewModel.SelectedConnectionGroup = viewModel.GroupOptions.Single(option => option.Id == group.Id);

        Assert.Equal(group.Id, connection.GroupId);
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
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService());

        Assert.IsType<ConnectionGroupNode>(Assert.Single(viewModel.RootNodes));

        viewModel.ManageGroupsCommand.Execute(new Avalonia.Controls.Window()).Subscribe();

        Assert.Null(connection.GroupId);
        Assert.IsAssignableFrom<ConnectionSettings>(Assert.Single(viewModel.RootNodes));
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
