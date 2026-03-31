using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class SqliteConnectionSettingsRepositoryTests : IDisposable
{
    private sealed class TrackingSecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _secrets = new();

        public bool IsAvailable => true;
        public string? UnavailableReason => null;
        public int GetCount { get; private set; }

        public Task SaveAsync(string key, string secret)
        {
            _secrets[key] = secret;
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string key)
        {
            GetCount++;
            return Task.FromResult(_secrets.TryGetValue(key, out var secret) ? secret : null);
        }

        public Task DeleteAsync(string key)
        {
            _secrets.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class UnavailableSecretStore : ISecretStore
    {
        public bool IsAvailable => false;
        public string? UnavailableReason => "Secure storage unavailable for tests.";
        public Task SaveAsync(string key, string secret) => throw new InvalidOperationException(UnavailableReason);
        public Task<string?> GetAsync(string key) => Task.FromResult<string?>(null);
        public Task DeleteAsync(string key) => Task.CompletedTask;
    }

    private readonly string _tempDirectory;

    public SqliteConnectionSettingsRepositoryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DataDeveloperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void SaveAll_AndLoadAll_RoundTripsSqlServerAndMySqlConnections()
    {
        var secretStore = new InMemorySecretStore();
        var repository = CreateRepository(secretStore: secretStore);
        var sqlServer = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            CredentialId = Guid.NewGuid(),
            Name = "NassServer",
            DatabaseType = DatabaseType.SqlServer,
            Server = "sql.local",
            Database = "master",
            User = "sa",
            Password = "secret",
            Encrypt = true,
            TrustServerCertificate = false
        };
        var mySql = new MySqlConnectionSettings
        {
            Id = Guid.NewGuid(),
            CredentialId = Guid.NewGuid(),
            Name = "repres nass-server",
            DatabaseType = DatabaseType.MySql,
            Server = "mysql.local",
            Database = "repres",
            Port = 3307,
            User = "root",
            Password = "mysql-secret",
            Encrypt = false,
            TrustServerCertificate = true
        };

        repository.SaveAll([sqlServer, mySql]);

        var loaded = repository.LoadAll();

        Assert.Equal(2, loaded.Count);
        var loadedSqlServer = Assert.IsType<SqlServerConnectionSettings>(loaded.Single(item => item.Id == sqlServer.Id));
        Assert.Equal(sqlServer.CredentialId, loadedSqlServer.CredentialId);
        Assert.Equal(sqlServer.Server, loadedSqlServer.Server);
        Assert.Equal(sqlServer.Database, loadedSqlServer.Database);
        repository.LoadPassword(loadedSqlServer);
        Assert.Equal(sqlServer.Password, loadedSqlServer.Password);

        var loadedMySql = Assert.IsType<MySqlConnectionSettings>(loaded.Single(item => item.Id == mySql.Id));
        Assert.Equal(mySql.CredentialId, loadedMySql.CredentialId);
        Assert.Equal(mySql.Server, loadedMySql.Server);
        Assert.Equal(mySql.Database, loadedMySql.Database);
        Assert.Equal(mySql.Port, loadedMySql.Port);
        repository.LoadPassword(loadedMySql);
        Assert.Equal(mySql.Password, loadedMySql.Password);
    }

    [Fact]
    public void LoadAll_DoesNotResolvePasswords_UntilRequested()
    {
        var secretStore = new TrackingSecretStore();
        var repository = CreateRepository(secretStore: secretStore);
        var sqlServer = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            CredentialId = Guid.NewGuid(),
            Name = "NassServer",
            DatabaseType = DatabaseType.SqlServer,
            Server = "sql.local",
            Database = "master",
            User = "sa",
            Password = "secret"
        };

        repository.SaveAll([sqlServer]);

        var loaded = repository.LoadAll();
        var loadedSqlServer = Assert.IsType<SqlServerConnectionSettings>(Assert.Single(loaded));

        Assert.Equal(0, secretStore.GetCount);
        Assert.Equal(string.Empty, loadedSqlServer.Password);

        repository.LoadPassword(loadedSqlServer);

        Assert.Equal(1, secretStore.GetCount);
        Assert.Equal("secret", loadedSqlServer.Password);
    }

    [Fact]
    public void SaveAll_Throws_WhenSecretStoreIsUnavailable_AndPasswordIsProvided()
    {
        var repository = CreateRepository(secretStore: new UnavailableSecretStore());
        var connection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            Name = "SQL",
            DatabaseType = DatabaseType.SqlServer,
            Server = "sql.local",
            Database = "master",
            User = "sa",
            Password = "secret"
        };

        var exception = Assert.Throws<InvalidOperationException>(() => repository.SaveAll([connection]));

        Assert.Equal("Secure storage unavailable for tests.", exception.Message);
    }

    [Fact]
    public void Save_PreservesExistingConnectionsWithoutReloadingTheirPasswords()
    {
        var secretStore = new TrackingSecretStore();
        var repository = CreateRepository(secretStore: secretStore);
        var first = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            CredentialId = Guid.NewGuid(),
            Name = "First",
            DatabaseType = DatabaseType.SqlServer,
            Server = "sql.local",
            Database = "master",
            User = "sa",
            Password = "first-secret"
        };
        var second = new MySqlConnectionSettings
        {
            Id = Guid.NewGuid(),
            CredentialId = Guid.NewGuid(),
            Name = "Second",
            DatabaseType = DatabaseType.MySql,
            Server = "mysql.local",
            Database = "repres",
            Port = 3306,
            User = "root",
            Password = "second-secret"
        };

        repository.SaveAll([first, second]);

        var loaded = repository.LoadAll();
        var loadedSecond = Assert.IsType<MySqlConnectionSettings>(loaded.Single(item => item.Id == second.Id));
        loadedSecond.Name = "Second updated";

        repository.Save(loadedSecond);

        var reloaded = repository.LoadAll();
        var reloadedFirst = Assert.IsType<SqlServerConnectionSettings>(reloaded.Single(item => item.Id == first.Id));
        var reloadedSecond = Assert.IsType<MySqlConnectionSettings>(reloaded.Single(item => item.Id == second.Id));

        repository.LoadPassword(reloadedFirst);
        repository.LoadPassword(reloadedSecond);

        Assert.Equal("first-secret", reloadedFirst.Password);
        Assert.Equal("second-secret", reloadedSecond.Password);
        Assert.Equal("Second updated", reloadedSecond.Name);
    }

    [Fact]
    public void LoadPassword_DoesNotOverwriteClearedPassword_WhenAlreadyLoaded()
    {
        var repository = CreateRepository();
        var connection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            CredentialId = Guid.NewGuid(),
            Name = "SQL",
            DatabaseType = DatabaseType.SqlServer,
            Server = "sql.local",
            Database = "master",
            User = "sa",
            Password = "secret"
        };

        repository.Save(connection);

        var loaded = Assert.IsType<SqlServerConnectionSettings>(Assert.Single(repository.LoadAll()));
        repository.LoadPassword(loaded);
        loaded.Password = string.Empty;

        repository.LoadPassword(loaded);

        Assert.Equal(string.Empty, loaded.Password);
        Assert.True(loaded.IsPasswordLoaded);
    }

    private SqliteConnectionSettingsRepository CreateRepository(ISecretStore? secretStore = null)
    {
        return new SqliteConnectionSettingsRepository(
            Path.Combine(_tempDirectory, "DataDeveloper.db"),
            secretStore ?? new InMemorySecretStore());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }
}
