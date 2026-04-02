using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Threading.Tasks;
using DataDeveloper.Data.Enums;
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

        public Task<string?> ShowSaveFileDialogAsync(string? suggestedName = null, string? title = null) => Task.FromResult<string?>(null);

        public Task<string?> ShowOpenFileAsync(string? title = null) => Task.FromResult<string?>(null);

        public Task<string?> ShowOpenDatabaseFileAsync(string? title = null) => Task.FromResult(OpenDatabaseFileResult);

        public Task<string?> ShowCreateDatabaseFileAsync(string? suggestedName = null, string? title = null) => Task.FromResult(CreateDatabaseFileResult);
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
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService());

        viewModel.SelectedConnectionFilter = viewModel.AvailableConnectionFilters.Single(option => option.DatabaseType == DatabaseType.Oracle);

        await viewModel.AddCommand.Execute().ToTask();

        var connection = Assert.IsType<OracleConnectionSettings>(viewModel.SelectedConnection);
        Assert.Equal(1521, connection.Port);
        Assert.Equal(DatabaseType.Oracle, connection.DatabaseType);
    }

    [Fact]
    public async Task AddCommand_CreatesSqLiteConnection_WhenSqLiteTypeSelected()
    {
        var viewModel = new ConnectionSelectorViewModel(
            new FakeConnectionSettingsRepository(),
            new DatabaseProviderFactoryService(),
            new InMemorySecretStore(),
            new FakeDialogService());

        viewModel.SelectedConnectionFilter = viewModel.AvailableConnectionFilters.Single(option => option.DatabaseType == DatabaseType.SqLite);

        await viewModel.AddCommand.Execute().ToTask();

        var connection = Assert.IsType<SqLiteConnectionSettings>(viewModel.SelectedConnection);
        Assert.Equal(DatabaseType.SqLite, connection.DatabaseType);
        Assert.Equal(string.Empty, connection.Database);
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
}
