using System.Text.Json;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class ConnectionExportServiceTests : IDisposable
{
    private sealed class FakeConnectionSettingsRepository : IConnectionSettingsRepository
    {
        public int LoadPasswordCallCount { get; private set; }

        public IReadOnlyList<ConnectionSettings> LoadAll() => [];

        public void LoadPassword(ConnectionSettings connectionSettings)
        {
            LoadPasswordCallCount++;
            connectionSettings.Password = "resolved-secret";
            connectionSettings.IsPasswordLoaded = true;
        }

        public void Save(ConnectionSettings connectionSettings)
        {
        }

        public void Delete(ConnectionSettings connectionSettings)
        {
        }

        public void SaveAll(IEnumerable<ConnectionSettings> connections)
        {
        }
    }

    private readonly string _tempDirectory;

    public ConnectionExportServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DataDeveloperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task ExportAsync_WithoutPasswords_OmitsPasswordAndDoesNotResolveIt()
    {
        var repository = new FakeConnectionSettingsRepository();
        var service = new ConnectionExportService(repository);
        var connection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            Name = "Prod",
            DatabaseType = DatabaseType.SqlServer,
            Server = "sql.local",
            Database = "master",
            Port = 1433,
            User = "sa",
            Password = "should-not-be-exported"
        };
        var filePath = Path.Combine(_tempDirectory, "export.json");

        await service.ExportAsync([connection], filePath, includePasswords: false);

        Assert.Equal(0, repository.LoadPasswordCallCount);
        var json = await File.ReadAllTextAsync(filePath);
        Assert.DoesNotContain("should-not-be-exported", json);
        Assert.DoesNotContain("resolved-secret", json);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(ConnectionExportFile.ExpectedExportedBy, document.RootElement.GetProperty("ExportedBy").GetString());
        Assert.Equal(ConnectionExportFile.CurrentFormatVersion, document.RootElement.GetProperty("FormatVersion").GetInt32());
        var entry = document.RootElement.GetProperty("Connections")[0];
        Assert.False(entry.TryGetProperty("Password", out _));
    }

    [Fact]
    public async Task ExportAsync_WithPasswords_ResolvesAndIncludesPassword()
    {
        var repository = new FakeConnectionSettingsRepository();
        var service = new ConnectionExportService(repository);
        var connection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            Name = "Prod",
            DatabaseType = DatabaseType.SqlServer,
            Server = "sql.local",
            Database = "master",
            Port = 1433,
            User = "sa"
        };
        var filePath = Path.Combine(_tempDirectory, "export.json");

        await service.ExportAsync([connection], filePath, includePasswords: true);

        Assert.Equal(1, repository.LoadPasswordCallCount);
        var json = await File.ReadAllTextAsync(filePath);

        using var document = JsonDocument.Parse(json);
        var entry = document.RootElement.GetProperty("Connections")[0];
        Assert.Equal("resolved-secret", entry.GetProperty("Password").GetString());
    }

    [Fact]
    public async Task ExportAsync_NeverIncludesGroupInformation()
    {
        var repository = new FakeConnectionSettingsRepository();
        var service = new ConnectionExportService(repository);
        var groupedConnection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            Name = "Grouped",
            DatabaseType = DatabaseType.SqlServer,
            Server = "sql.local",
            Database = "master",
            User = "sa"
        };
        var filePath = Path.Combine(_tempDirectory, "export.json");

        await service.ExportAsync([groupedConnection], filePath, includePasswords: false);

        var json = await File.ReadAllTextAsync(filePath);
        Assert.DoesNotContain("GroupName", json);
        Assert.DoesNotContain("GroupId", json);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }
}
