using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.Oracle;
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.PostgresSql;
using DataDeveloper.Data.Providers.SqLite;
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
            AuthenticationMode = SqlServerAuthenticationMode.WindowsIntegrated,
            User = "sa",
            Password = "secret",
            StatementTimeoutSeconds = 120,
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
            StatementTimeoutSeconds = 240,
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
        Assert.Equal(sqlServer.AuthenticationMode, loadedSqlServer.AuthenticationMode);
        Assert.Equal(sqlServer.StatementTimeoutSeconds, loadedSqlServer.StatementTimeoutSeconds);
        repository.LoadPassword(loadedSqlServer);
        Assert.Equal(sqlServer.Password, loadedSqlServer.Password);

        var loadedMySql = Assert.IsType<MySqlConnectionSettings>(loaded.Single(item => item.Id == mySql.Id));
        Assert.Equal(mySql.CredentialId, loadedMySql.CredentialId);
        Assert.Equal(mySql.Server, loadedMySql.Server);
        Assert.Equal(mySql.Database, loadedMySql.Database);
        Assert.Equal(mySql.Port, loadedMySql.Port);
        Assert.Equal(mySql.StatementTimeoutSeconds, loadedMySql.StatementTimeoutSeconds);
        repository.LoadPassword(loadedMySql);
        Assert.Equal(mySql.Password, loadedMySql.Password);
    }

    [Fact]
    public void LoadAll_UsesDefaultStatementTimeout_WhenValueIsMissingOrInvalid()
    {
        var repository = CreateRepository(secretStore: new InMemorySecretStore());
        var connection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            Name = "SQL",
            DatabaseType = DatabaseType.SqlServer,
            Server = "sql.local",
            Database = "master",
            User = "sa",
            Password = "secret",
            StatementTimeoutSeconds = 0
        };

        repository.Save(connection);

        var loaded = repository.LoadAll();
        var loadedSqlServer = Assert.IsType<SqlServerConnectionSettings>(Assert.Single(loaded));

        Assert.Equal(ConnectionSettings.DefaultStatementTimeoutSeconds, loadedSqlServer.StatementTimeoutSeconds);
    }

    [Fact]
    public void SaveAll_AndLoadAll_RoundTripsPostgresConnection()
    {
        var secretStore = new InMemorySecretStore();
        var repository = CreateRepository(secretStore: secretStore);
        var postgres = new PostgresConnectionSettings
        {
            Id = Guid.NewGuid(),
            CredentialId = Guid.NewGuid(),
            Name = "Postgres",
            DatabaseType = DatabaseType.PostgresSql,
            Server = "postgres.local",
            Database = "app",
            Port = 5433,
            User = "postgres",
            Password = "postgres-secret",
            Encrypt = true,
            TrustServerCertificate = false
        };

        repository.SaveAll([postgres]);

        var loaded = repository.LoadAll();

        var loadedPostgres = Assert.IsType<PostgresConnectionSettings>(Assert.Single(loaded));
        Assert.Equal(postgres.CredentialId, loadedPostgres.CredentialId);
        Assert.Equal(postgres.Server, loadedPostgres.Server);
        Assert.Equal(postgres.Database, loadedPostgres.Database);
        Assert.Equal(postgres.Port, loadedPostgres.Port);
        repository.LoadPassword(loadedPostgres);
        Assert.Equal(postgres.Password, loadedPostgres.Password);
    }

    [Fact]
    public void SaveAll_AndLoadAll_RoundTripsOracleAndSqLiteConnections()
    {
        var secretStore = new InMemorySecretStore();
        var repository = CreateRepository(secretStore: secretStore);
        var oracle = new OracleConnectionSettings
        {
            Id = Guid.NewGuid(),
            CredentialId = Guid.NewGuid(),
            Name = "Oracle",
            DatabaseType = DatabaseType.Oracle,
            Server = "oracle.local",
            Database = "xe",
            Port = 1522,
            User = "system",
            Password = "oracle-secret"
        };
        var sqLite = new SqLiteConnectionSettings
        {
            Id = Guid.NewGuid(),
            Name = "SQLite",
            DatabaseType = DatabaseType.SqLite,
            Database = "/tmp/app.db"
        };

        repository.SaveAll([oracle, sqLite]);

        var loaded = repository.LoadAll();

        var loadedOracle = Assert.IsType<OracleConnectionSettings>(loaded.Single(item => item.Id == oracle.Id));
        Assert.Equal(oracle.CredentialId, loadedOracle.CredentialId);
        Assert.Equal(oracle.Server, loadedOracle.Server);
        Assert.Equal(oracle.Database, loadedOracle.Database);
        Assert.Equal(oracle.Port, loadedOracle.Port);
        repository.LoadPassword(loadedOracle);
        Assert.Equal(oracle.Password, loadedOracle.Password);

        var loadedSqLite = Assert.IsType<SqLiteConnectionSettings>(loaded.Single(item => item.Id == sqLite.Id));
        Assert.Equal(sqLite.Database, loadedSqLite.Database);
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
    public void LoadAll_DefaultsSqlServerAuthenticationMode_WhenColumnIsMissing()
    {
        var databasePath = Path.Combine(_tempDirectory, "legacy.db");
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath}"))
        {
            connection.Open();
            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = """
                                        create table app_connection
                                        (
                                            id text not null primary key,
                                            credential_id text null,
                                            name text not null,
                                            database_type integer not null,
                                            user_name text not null,
                                            encrypt integer not null,
                                            trust_server_certificate integer not null,
                                            allow_blank_password integer not null,
                                            server text not null,
                                            database_name text not null,
                                            port integer null,
                                            created_at text not null,
                                            updated_at text not null
                                        );
                                        insert into app_connection
                                        (
                                            id,
                                            credential_id,
                                            name,
                                            database_type,
                                            user_name,
                                            encrypt,
                                            trust_server_certificate,
                                            allow_blank_password,
                                            server,
                                            database_name,
                                            port,
                                            created_at,
                                            updated_at
                                        )
                                        values
                                        (
                                            '11111111-1111-1111-1111-111111111111',
                                            null,
                                            'Legacy SQL',
                                            0,
                                            'sa',
                                            1,
                                            0,
                                            0,
                                            'sql.local',
                                            'master',
                                            null,
                                            '2026-04-08T00:00:00.0000000Z',
                                            '2026-04-08T00:00:00.0000000Z'
                                        );
                                        """;
            createCommand.ExecuteNonQuery();
        }

        var repository = new SqliteConnectionSettingsRepository(databasePath, new InMemorySecretStore());

        var loaded = Assert.IsType<SqlServerConnectionSettings>(Assert.Single(repository.LoadAll()));

        Assert.Equal(SqlServerAuthenticationMode.SqlLogin, loaded.AuthenticationMode);
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
