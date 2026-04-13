using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using DataDeveloper.Core;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.Oracle;
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.PostgresSql;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Interfaces;
using Microsoft.Data.Sqlite;

namespace DataDeveloper.Services;

public class SqliteConnectionSettingsRepository : IConnectionSettingsRepository
{
    private const string DatabaseFolder = "state";
    private const string DatabaseFileName = "DataDeveloper.db";

    private readonly string _databasePath;
    private readonly ISecretStore _secretStore;
    private bool _isInitialized;

    public SqliteConnectionSettingsRepository(AppDataFileService fileService, ISecretStore secretStore)
        : this(fileService.GetFullPath(DatabaseFileName, DatabaseFolder), secretStore)
    {
    }

    public SqliteConnectionSettingsRepository(string databasePath, ISecretStore secretStore)
    {
        _databasePath = databasePath;
        _secretStore = secretStore;
    }

    public IReadOnlyList<ConnectionSettings> LoadAll()
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              select
                                  id,
                                  credential_id,
                                  name,
                                  database_type,
                                  sql_server_authentication_mode,
                                  user_name,
                                  encrypt,
                                  trust_server_certificate,
                                  allow_blank_password,
                                  statement_timeout_seconds,
                                  dml_transaction_mode,
                                  server,
                                  database_name,
                                  port
                              from app_connection
                              order by name;
                              """;

        using var reader = command.ExecuteReader();
        var connections = new List<ConnectionSettings>();
        while (reader.Read())
        {
            var databaseType = (DatabaseType)reader.GetInt32(reader.GetOrdinal("database_type"));
            var connectionSettings = CreateConnectionSettings(databaseType);

            connectionSettings.Id = Guid.Parse(reader.GetString(reader.GetOrdinal("id")));
            connectionSettings.CredentialId = TryGetGuid(reader, "credential_id");
            connectionSettings.Name = reader.GetString(reader.GetOrdinal("name"));
            connectionSettings.User = reader.GetString(reader.GetOrdinal("user_name"));
            connectionSettings.Password = string.Empty;
            connectionSettings.IsPasswordLoaded = false;
            connectionSettings.LoadedPasswordSnapshot = null;
            connectionSettings.Encrypt = reader.GetBoolean(reader.GetOrdinal("encrypt"));
            connectionSettings.TrustServerCertificate = reader.GetBoolean(reader.GetOrdinal("trust_server_certificate"));
            connectionSettings.AllowBlankPassword = reader.GetBoolean(reader.GetOrdinal("allow_blank_password"));
            connectionSettings.StatementTimeoutSeconds = reader.IsDBNull(reader.GetOrdinal("statement_timeout_seconds"))
                ? ConnectionSettings.DefaultStatementTimeoutSeconds
                : NormalizeStatementTimeout(reader.GetInt32(reader.GetOrdinal("statement_timeout_seconds")));
            connectionSettings.DmlTransactionMode = reader.IsDBNull(reader.GetOrdinal("dml_transaction_mode"))
                ? ConnectionSettings.GetDefaultDmlTransactionMode(databaseType)
                : (DmlTransactionMode)reader.GetInt32(reader.GetOrdinal("dml_transaction_mode"));

            switch (connectionSettings)
            {
                case SqlServerConnectionSettings sqlServer:
                    sqlServer.AuthenticationMode = reader.IsDBNull(reader.GetOrdinal("sql_server_authentication_mode"))
                        ? SqlServerAuthenticationMode.SqlLogin
                        : (SqlServerAuthenticationMode)reader.GetInt32(reader.GetOrdinal("sql_server_authentication_mode"));
                    sqlServer.Server = reader.GetString(reader.GetOrdinal("server"));
                    sqlServer.Database = reader.GetString(reader.GetOrdinal("database_name"));
                    sqlServer.Port = reader.IsDBNull(reader.GetOrdinal("port")) ? 1433 : Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("port")));
                    break;
                case MySqlConnectionSettings mySql:
                    mySql.Server = reader.GetString(reader.GetOrdinal("server"));
                    mySql.Database = reader.GetString(reader.GetOrdinal("database_name"));
                    mySql.Port = reader.IsDBNull(reader.GetOrdinal("port")) ? 3306u : Convert.ToUInt32(reader.GetInt64(reader.GetOrdinal("port")));
                    break;
                case OracleConnectionSettings oracle:
                    oracle.Server = reader.GetString(reader.GetOrdinal("server"));
                    oracle.Database = reader.GetString(reader.GetOrdinal("database_name"));
                    oracle.Port = reader.IsDBNull(reader.GetOrdinal("port")) ? 1521 : Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("port")));
                    break;
                case PostgresConnectionSettings postgres:
                    postgres.Server = reader.GetString(reader.GetOrdinal("server"));
                    postgres.Database = reader.GetString(reader.GetOrdinal("database_name"));
                    postgres.Port = reader.IsDBNull(reader.GetOrdinal("port")) ? 5432 : Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("port")));
                    break;
                case SqLiteConnectionSettings sqLite:
                    sqLite.Database = reader.GetString(reader.GetOrdinal("database_name"));
                    break;
            }

            connections.Add(connectionSettings);
        }

        return connections;
    }

    public void LoadPassword(ConnectionSettings connectionSettings)
    {
        ArgumentNullException.ThrowIfNull(connectionSettings);

        if (connectionSettings.IsPasswordLoaded)
            return;

        if (connectionSettings.CredentialId is null)
        {
            connectionSettings.IsPasswordLoaded = true;
            connectionSettings.LoadedPasswordSnapshot = connectionSettings.Password;
            return;
        }

        connectionSettings.Password = ResolvePassword(connectionSettings.CredentialId);
        connectionSettings.IsPasswordLoaded = true;
        connectionSettings.LoadedPasswordSnapshot = connectionSettings.Password;
    }

    public void SaveAll(IEnumerable<ConnectionSettings> connections)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        SaveAll(connection, connections, _secretStore);
    }

    private static void SaveAll(SqliteConnection connection, IEnumerable<ConnectionSettings> connections, ISecretStore secretStore)
    {
        using var transaction = connection.BeginTransaction();

        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "delete from app_connection;";
            deleteCommand.ExecuteNonQuery();
        }

        foreach (var item in connections)
        {
            if (!string.IsNullOrWhiteSpace(item.Password))
            {
                if (!secretStore.IsAvailable)
                    throw new InvalidOperationException(secretStore.UnavailableReason ?? "Secure credential storage is not available.");

                item.CredentialId ??= Guid.NewGuid();
                secretStore.SaveAsync(item.CredentialId.Value.ToString(), item.Password).GetAwaiter().GetResult();
            }
            else if (item.CredentialId is not null)
            {
                secretStore.DeleteAsync(item.CredentialId.Value.ToString()).GetAwaiter().GetResult();
                item.CredentialId = null;
            }

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                                  insert into app_connection
                                  (
                                      id,
                                      credential_id,
                                      name,
                                      database_type,
                                      sql_server_authentication_mode,
                                      user_name,
                                      encrypt,
                                      trust_server_certificate,
                                      allow_blank_password,
                                      statement_timeout_seconds,
                                      dml_transaction_mode,
                                      server,
                                      database_name,
                                      port,
                                      created_at,
                                      updated_at
                                  )
                                  values
                                  (
                                      $id,
                                      $credentialId,
                                      $name,
                                      $databaseType,
                                      $sqlServerAuthenticationMode,
                                      $userName,
                                      $encrypt,
                                      $trustServerCertificate,
                                      $allowBlankPassword,
                                      $statementTimeoutSeconds,
                                      $dmlTransactionMode,
                                      $server,
                                      $databaseName,
                                      $port,
                                      $createdAt,
                                      $updatedAt
                                  );
                                  """;

            var now = DateTime.UtcNow.ToString("O");
            command.Parameters.AddWithValue("$id", item.Id == Guid.Empty ? Guid.NewGuid().ToString() : item.Id.ToString());
            command.Parameters.AddWithValue("$credentialId", item.CredentialId?.ToString() ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$name", item.Name);
            command.Parameters.AddWithValue("$databaseType", (int)item.DatabaseType);
            command.Parameters.AddWithValue("$sqlServerAuthenticationMode", GetSqlServerAuthenticationMode(item));
            command.Parameters.AddWithValue("$userName", item.User);
            command.Parameters.AddWithValue("$encrypt", item.Encrypt ? 1 : 0);
            command.Parameters.AddWithValue("$trustServerCertificate", item.TrustServerCertificate ? 1 : 0);
            command.Parameters.AddWithValue("$allowBlankPassword", item.AllowBlankPassword ? 1 : 0);
            command.Parameters.AddWithValue("$statementTimeoutSeconds", NormalizeStatementTimeout(item.StatementTimeoutSeconds));
            command.Parameters.AddWithValue("$dmlTransactionMode", (int)item.DmlTransactionMode);
            command.Parameters.AddWithValue("$server", GetServer(item));
            command.Parameters.AddWithValue("$databaseName", GetDatabase(item));
            command.Parameters.AddWithValue("$port", GetPort(item));
            command.Parameters.AddWithValue("$createdAt", now);
            command.Parameters.AddWithValue("$updatedAt", now);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void Save(ConnectionSettings connectionSettings)
    {
        ArgumentNullException.ThrowIfNull(connectionSettings);

        EnsureInitialized();

        if (!string.IsNullOrWhiteSpace(connectionSettings.Password))
        {
            if (connectionSettings.CredentialId is null ||
                !string.Equals(connectionSettings.Password, connectionSettings.LoadedPasswordSnapshot, StringComparison.Ordinal))
            {
                if (!_secretStore.IsAvailable)
                    throw new InvalidOperationException(_secretStore.UnavailableReason ?? "Secure credential storage is not available.");

                connectionSettings.CredentialId ??= Guid.NewGuid();
                _secretStore.SaveAsync(connectionSettings.CredentialId.Value.ToString(), connectionSettings.Password).GetAwaiter().GetResult();
            }

            connectionSettings.IsPasswordLoaded = true;
            connectionSettings.LoadedPasswordSnapshot = connectionSettings.Password;
        }
        else if (connectionSettings.CredentialId is not null && connectionSettings.IsPasswordLoaded)
        {
            _secretStore.DeleteAsync(connectionSettings.CredentialId.Value.ToString()).GetAwaiter().GetResult();
            connectionSettings.CredentialId = null;
            connectionSettings.LoadedPasswordSnapshot = string.Empty;
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              insert into app_connection
                              (
                                  id,
                                  credential_id,
                                  name,
                                  database_type,
                                  sql_server_authentication_mode,
                                  user_name,
                                  encrypt,
                                  trust_server_certificate,
                                  allow_blank_password,
                                  statement_timeout_seconds,
                                  dml_transaction_mode,
                                  server,
                                  database_name,
                                  port,
                                  created_at,
                                  updated_at
                              )
                              values
                              (
                                  $id,
                                  $credentialId,
                                  $name,
                                  $databaseType,
                                  $sqlServerAuthenticationMode,
                                  $userName,
                                  $encrypt,
                                  $trustServerCertificate,
                                  $allowBlankPassword,
                                  $statementTimeoutSeconds,
                                  $dmlTransactionMode,
                                  $server,
                                  $databaseName,
                                  $port,
                                  $createdAt,
                                  $updatedAt
                              )
                              on conflict(id) do update set
                                  credential_id = excluded.credential_id,
                                  name = excluded.name,
                                  database_type = excluded.database_type,
                                  sql_server_authentication_mode = excluded.sql_server_authentication_mode,
                                  user_name = excluded.user_name,
                                  encrypt = excluded.encrypt,
                                  trust_server_certificate = excluded.trust_server_certificate,
                                  allow_blank_password = excluded.allow_blank_password,
                                  statement_timeout_seconds = excluded.statement_timeout_seconds,
                                  dml_transaction_mode = excluded.dml_transaction_mode,
                                  server = excluded.server,
                                  database_name = excluded.database_name,
                                  port = excluded.port,
                                  updated_at = excluded.updated_at;
                              """;

        var id = connectionSettings.Id == Guid.Empty ? Guid.NewGuid() : connectionSettings.Id;
        connectionSettings.Id = id;
        var now = DateTime.UtcNow.ToString("O");
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$credentialId", connectionSettings.CredentialId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$name", connectionSettings.Name);
        command.Parameters.AddWithValue("$databaseType", (int)connectionSettings.DatabaseType);
        command.Parameters.AddWithValue("$sqlServerAuthenticationMode", GetSqlServerAuthenticationMode(connectionSettings));
        command.Parameters.AddWithValue("$userName", connectionSettings.User);
        command.Parameters.AddWithValue("$encrypt", connectionSettings.Encrypt ? 1 : 0);
        command.Parameters.AddWithValue("$trustServerCertificate", connectionSettings.TrustServerCertificate ? 1 : 0);
        command.Parameters.AddWithValue("$allowBlankPassword", connectionSettings.AllowBlankPassword ? 1 : 0);
        command.Parameters.AddWithValue("$statementTimeoutSeconds", NormalizeStatementTimeout(connectionSettings.StatementTimeoutSeconds));
        command.Parameters.AddWithValue("$dmlTransactionMode", (int)connectionSettings.DmlTransactionMode);
        command.Parameters.AddWithValue("$server", GetServer(connectionSettings));
        command.Parameters.AddWithValue("$databaseName", GetDatabase(connectionSettings));
        command.Parameters.AddWithValue("$port", GetPort(connectionSettings));
        command.Parameters.AddWithValue("$createdAt", now);
        command.Parameters.AddWithValue("$updatedAt", now);
        command.ExecuteNonQuery();
    }

    public void Delete(ConnectionSettings connectionSettings)
    {
        ArgumentNullException.ThrowIfNull(connectionSettings);

        EnsureInitialized();

        if (connectionSettings.CredentialId is not null)
            _secretStore.DeleteAsync(connectionSettings.CredentialId.Value.ToString()).GetAwaiter().GetResult();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "delete from app_connection where id = $id;";
        command.Parameters.AddWithValue("$id", connectionSettings.Id.ToString());
        command.ExecuteNonQuery();
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
            return;

        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              create table if not exists app_connection
                              (
                                  id text not null primary key,
                                  credential_id text null,
                                  name text not null,
                                  database_type integer not null,
                                  sql_server_authentication_mode integer not null default 0,
                                  user_name text not null,
                                  encrypt integer not null,
                                  trust_server_certificate integer not null,
                                  allow_blank_password integer not null,
                                  statement_timeout_seconds integer not null default 60,
                                  dml_transaction_mode integer null,
                                  server text not null,
                                  database_name text not null,
                                  port integer null,
                                  created_at text not null,
                                  updated_at text not null
                              );
                              """;
        command.ExecuteNonQuery();
        EnsureColumnExists(connection, "sql_server_authentication_mode", "integer not null default 0");
        EnsureColumnExists(connection, "statement_timeout_seconds", "integer not null default 60");
        EnsureColumnExists(connection, "dml_transaction_mode", "integer null");

        _isInitialized = true;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();
        return connection;
    }

    private static ConnectionSettings CreateConnectionSettings(DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.SqlServer => new SqlServerConnectionSettings
            {
                DatabaseType = DatabaseType.SqlServer,
                DmlTransactionMode = ConnectionSettings.GetDefaultDmlTransactionMode(DatabaseType.SqlServer)
            },
            DatabaseType.Oracle => new OracleConnectionSettings
            {
                DatabaseType = DatabaseType.Oracle,
                DmlTransactionMode = ConnectionSettings.GetDefaultDmlTransactionMode(DatabaseType.Oracle)
            },
            DatabaseType.PostgresSql => new PostgresConnectionSettings
            {
                DatabaseType = DatabaseType.PostgresSql,
                DmlTransactionMode = ConnectionSettings.GetDefaultDmlTransactionMode(DatabaseType.PostgresSql)
            },
            DatabaseType.MySql => new MySqlConnectionSettings
            {
                DatabaseType = DatabaseType.MySql,
                DmlTransactionMode = ConnectionSettings.GetDefaultDmlTransactionMode(DatabaseType.MySql)
            },
            DatabaseType.SqLite => new SqLiteConnectionSettings
            {
                DatabaseType = DatabaseType.SqLite,
                DmlTransactionMode = ConnectionSettings.GetDefaultDmlTransactionMode(DatabaseType.SqLite)
            },
            _ => throw new NotSupportedException($"Database type {databaseType} is not supported.")
        };
    }

    private static Guid? TryGetGuid(IDataRecord record, string columnName)
    {
        var ordinal = record.GetOrdinal(columnName);
        return record.IsDBNull(ordinal) ? null : Guid.Parse(record.GetString(ordinal));
    }

    private string ResolvePassword(Guid? credentialId)
    {
        if (credentialId is not null)
        {
            var secret = _secretStore.GetAsync(credentialId.Value.ToString()).GetAwaiter().GetResult();
            if (secret is not null)
                return secret;
        }

        return string.Empty;
    }

    private static string GetServer(ConnectionSettings connectionSettings) =>
        connectionSettings switch
        {
            SqlServerConnectionSettings sqlServer => sqlServer.Server,
            OracleConnectionSettings oracle => oracle.Server,
            PostgresConnectionSettings postgres => postgres.Server,
            MySqlConnectionSettings mySql => mySql.Server,
            _ => string.Empty
        };

    private static string GetDatabase(ConnectionSettings connectionSettings) =>
        connectionSettings switch
        {
            SqlServerConnectionSettings sqlServer => sqlServer.Database,
            OracleConnectionSettings oracle => oracle.Database,
            PostgresConnectionSettings postgres => postgres.Database,
            MySqlConnectionSettings mySql => mySql.Database,
            SqLiteConnectionSettings sqLite => sqLite.Database,
            _ => string.Empty
        };

    private static object GetPort(ConnectionSettings connectionSettings) =>
        connectionSettings switch
        {
            SqlServerConnectionSettings sqlServer => sqlServer.Port,
            OracleConnectionSettings oracle => oracle.Port,
            PostgresConnectionSettings postgres => postgres.Port,
            MySqlConnectionSettings mySql => (long)mySql.Port,
            _ => DBNull.Value
        };

    private static int GetSqlServerAuthenticationMode(ConnectionSettings connectionSettings) =>
        connectionSettings is SqlServerConnectionSettings sqlServer
            ? (int)sqlServer.AuthenticationMode
            : (int)SqlServerAuthenticationMode.SqlLogin;

    private static int NormalizeStatementTimeout(int timeoutSeconds) =>
        timeoutSeconds > 0 ? timeoutSeconds : ConnectionSettings.DefaultStatementTimeoutSeconds;

    private static void EnsureColumnExists(SqliteConnection connection, string columnName, string columnDefinition)
    {
        using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "pragma table_info(app_connection);";
        using var reader = pragmaCommand.ExecuteReader();

        while (reader.Read())
        {
            if (string.Equals(reader.GetString(reader.GetOrdinal("name")), columnName, StringComparison.OrdinalIgnoreCase))
                return;
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"alter table app_connection add column {columnName} {columnDefinition};";
        alterCommand.ExecuteNonQuery();
    }
}
