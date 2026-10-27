using System;
using System.IO;
using DataDeveloper.Core;
using DataDeveloper.Data.Ai;
using DataDeveloper.Interfaces;
using Microsoft.Data.Sqlite;

namespace DataDeveloper.Services;

public class SqliteAiSettingsRepository : IAiSettingsRepository
{
    private const string DatabaseFolder = "state";
    private const string DatabaseFileName = "DataDeveloper.db";

    private readonly string _databasePath;
    private bool _isInitialized;

    public SqliteAiSettingsRepository(AppDataFileService fileService)
        : this(fileService.GetFullPath(DatabaseFileName, DatabaseFolder))
    {
    }

    public SqliteAiSettingsRepository(string databasePath)
    {
        _databasePath = databasePath;
    }

    public AiProviderSettings Load(AiProviderType providerType)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              select executable_path, model, timeout_seconds
                              from ai_provider_settings
                              where provider_type = $providerType;
                              """;
        command.Parameters.AddWithValue("$providerType", (int)providerType);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return new AiProviderSettings { ProviderType = providerType };

        return new AiProviderSettings
        {
            ProviderType = providerType,
            ExecutablePath = reader.IsDBNull(0) ? null : reader.GetString(0),
            Model = reader.IsDBNull(1) ? null : reader.GetString(1),
            TimeoutSeconds = AiProviderSettings.NormalizeTimeout(reader.GetInt32(2))
        };
    }

    public void Save(AiProviderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              insert into ai_provider_settings (provider_type, executable_path, model, timeout_seconds)
                              values ($providerType, $executablePath, $model, $timeoutSeconds)
                              on conflict(provider_type) do update set
                                  executable_path = excluded.executable_path,
                                  model = excluded.model,
                                  timeout_seconds = excluded.timeout_seconds;
                              """;
        command.Parameters.AddWithValue("$providerType", (int)settings.ProviderType);
        command.Parameters.AddWithValue("$executablePath", NullIfBlank(settings.ExecutablePath));
        command.Parameters.AddWithValue("$model", NullIfBlank(settings.Model));
        command.Parameters.AddWithValue("$timeoutSeconds", AiProviderSettings.NormalizeTimeout(settings.TimeoutSeconds));
        command.ExecuteNonQuery();
    }

    private static object NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

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
                              create table if not exists ai_provider_settings
                              (
                                  provider_type integer not null primary key,
                                  executable_path text null,
                                  model text null,
                                  timeout_seconds integer not null default 120
                              );
                              """;
        command.ExecuteNonQuery();

        _isInitialized = true;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();
        return connection;
    }
}
