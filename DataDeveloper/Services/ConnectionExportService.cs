using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.Oracle;
using DataDeveloper.Data.Providers.PostgresSql;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Interfaces;

namespace DataDeveloper.Services;

public sealed class ConnectionExportEntry
{
    public string Name { get; set; } = string.Empty;
    public string DatabaseType { get; set; } = string.Empty;
    public string? Server { get; set; }
    public int? Port { get; set; }
    public string? Database { get; set; }
    public string User { get; set; } = string.Empty;
    public string? Password { get; set; }
    public bool Encrypt { get; set; }
    public bool TrustServerCertificate { get; set; }
    public string? SqlServerAuthenticationMode { get; set; }
    public string DmlTransactionMode { get; set; } = string.Empty;
    public int StatementTimeoutSeconds { get; set; }
}

public sealed class ConnectionExportFile
{
    public const string ExpectedExportedBy = "DataDeveloper";
    public const int CurrentFormatVersion = 1;

    public string ExportedBy { get; set; } = ExpectedExportedBy;
    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public List<ConnectionExportEntry> Connections { get; set; } = new();
}

public class ConnectionExportService : IConnectionExportService
{
    private readonly IConnectionSettingsRepository _connectionSettingsRepository;

    public ConnectionExportService(IConnectionSettingsRepository connectionSettingsRepository)
    {
        _connectionSettingsRepository = connectionSettingsRepository;
    }

    public async Task ExportAsync(
        IReadOnlyList<ConnectionSettings> connections,
        string filePath,
        bool includePasswords)
    {
        // Groups are a personal, local organization scheme, not something meant to
        // travel with the connection; imported connections always land ungrouped.
        var entries = new List<ConnectionExportEntry>();
        foreach (var connection in connections)
        {
            if (includePasswords)
                await Task.Run(() => _connectionSettingsRepository.LoadPassword(connection));

            entries.Add(new ConnectionExportEntry
            {
                Name = connection.Name,
                DatabaseType = connection.DatabaseType.ToString(),
                Server = GetServer(connection),
                Port = GetPort(connection),
                Database = GetDatabase(connection),
                User = connection.User,
                Password = includePasswords ? connection.Password : null,
                Encrypt = connection.Encrypt,
                TrustServerCertificate = connection.TrustServerCertificate,
                SqlServerAuthenticationMode = connection is SqlServerConnectionSettings sqlServer
                    ? sqlServer.AuthenticationMode.ToString()
                    : null,
                DmlTransactionMode = connection.DmlTransactionMode.ToString(),
                StatementTimeoutSeconds = connection.StatementTimeoutSeconds
            });
        }

        var exportFile = new ConnectionExportFile { Connections = entries };

        var json = JsonSerializer.Serialize(exportFile, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        await File.WriteAllTextAsync(filePath, json);
    }

    private static string? GetServer(ConnectionSettings connection) => connection switch
    {
        SqlServerConnectionSettings sqlServer => sqlServer.Server,
        OracleConnectionSettings oracle => oracle.Server,
        PostgresConnectionSettings postgres => postgres.Server,
        MySqlConnectionSettings mySql => mySql.Server,
        _ => null
    };

    private static string? GetDatabase(ConnectionSettings connection) => connection switch
    {
        SqlServerConnectionSettings sqlServer => sqlServer.Database,
        OracleConnectionSettings oracle => oracle.Database,
        PostgresConnectionSettings postgres => postgres.Database,
        MySqlConnectionSettings mySql => mySql.Database,
        SqLiteConnectionSettings sqLite => sqLite.Database,
        _ => null
    };

    private static int? GetPort(ConnectionSettings connection) => connection switch
    {
        SqlServerConnectionSettings sqlServer => sqlServer.Port,
        OracleConnectionSettings oracle => oracle.Port,
        PostgresConnectionSettings postgres => postgres.Port,
        MySqlConnectionSettings mySql => (int)mySql.Port,
        _ => null
    };
}
