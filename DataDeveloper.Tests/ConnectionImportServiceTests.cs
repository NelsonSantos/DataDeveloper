using System.Text.Json;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class ConnectionImportServiceTests : IDisposable
{
    private sealed class FakeConnectionSettingsRepository : IConnectionSettingsRepository
    {
        private readonly List<ConnectionSettings> _connections;

        public FakeConnectionSettingsRepository(IEnumerable<ConnectionSettings>? connections = null)
        {
            _connections = connections?.ToList() ?? [];
        }

        public IReadOnlyList<ConnectionSettings> LoadAll() => _connections.ToList();

        public void LoadPassword(ConnectionSettings connectionSettings)
        {
        }

        public void Save(ConnectionSettings connectionSettings)
        {
            _connections.RemoveAll(c => c.Id == connectionSettings.Id);
            _connections.Add(connectionSettings);
        }

        public void Delete(ConnectionSettings connectionSettings)
        {
            _connections.Remove(connectionSettings);
        }

        public void SaveAll(IEnumerable<ConnectionSettings> connections)
        {
        }
    }

    private readonly string _tempDirectory;

    public ConnectionImportServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DataDeveloperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    private string WriteExportFile(ConnectionExportFile exportFile)
    {
        var filePath = Path.Combine(_tempDirectory, "import.json");
        File.WriteAllText(filePath, JsonSerializer.Serialize(exportFile));
        return filePath;
    }

    [Fact]
    public async Task ImportAsync_RejectsFile_WithWrongMarker()
    {
        var filePath = WriteExportFile(new ConnectionExportFile
        {
            ExportedBy = "SomeOtherApp",
            FormatVersion = ConnectionExportFile.CurrentFormatVersion,
            Connections = [new ConnectionExportEntry { Name = "X", DatabaseType = "SqlServer" }]
        });
        var service = new ConnectionImportService(new FakeConnectionSettingsRepository());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportAsync(filePath));
    }

    [Fact]
    public async Task ImportAsync_RejectsFile_WithNewerFormatVersion()
    {
        var filePath = WriteExportFile(new ConnectionExportFile
        {
            ExportedBy = ConnectionExportFile.ExpectedExportedBy,
            FormatVersion = ConnectionExportFile.CurrentFormatVersion + 1,
            Connections = [new ConnectionExportEntry { Name = "X", DatabaseType = "SqlServer" }]
        });
        var service = new ConnectionImportService(new FakeConnectionSettingsRepository());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportAsync(filePath));
    }

    [Fact]
    public async Task ImportAsync_AcceptsFile_WithOlderFormatVersion()
    {
        var filePath = WriteExportFile(new ConnectionExportFile
        {
            ExportedBy = ConnectionExportFile.ExpectedExportedBy,
            FormatVersion = ConnectionExportFile.CurrentFormatVersion - 1,
            Connections = [new ConnectionExportEntry { Name = "X", DatabaseType = "SqlServer" }]
        });
        var settingsRepository = new FakeConnectionSettingsRepository();
        var service = new ConnectionImportService(settingsRepository);

        var importedCount = await service.ImportAsync(filePath);

        Assert.Equal(1, importedCount);
        Assert.Single(settingsRepository.LoadAll());
    }

    [Fact]
    public async Task ImportAsync_ImportsConnection_WithFreshId_AndUngrouped()
    {
        var filePath = WriteExportFile(new ConnectionExportFile
        {
            Connections =
            [
                new ConnectionExportEntry
                {
                    Name = "Prod",
                    DatabaseType = "SqlServer",
                    Server = "sql.local",
                    Database = "master",
                    Port = 1433,
                    User = "sa"
                }
            ]
        });
        var settingsRepository = new FakeConnectionSettingsRepository();
        var service = new ConnectionImportService(settingsRepository);

        var importedCount = await service.ImportAsync(filePath);

        Assert.Equal(1, importedCount);
        var imported = Assert.Single(settingsRepository.LoadAll());
        var sqlServer = Assert.IsType<SqlServerConnectionSettings>(imported);
        Assert.NotEqual(Guid.Empty, sqlServer.Id);
        Assert.Equal("Prod", sqlServer.Name);
        Assert.Equal("sql.local", sqlServer.Server);
        Assert.Equal("master", sqlServer.Database);
        Assert.Equal(1433, sqlServer.Port);
        Assert.Null(sqlServer.GroupId);
    }

    [Fact]
    public async Task ImportAsync_ResolvesNameCollision_WithNumberedSuffix()
    {
        var existing = new SqlServerConnectionSettings { Id = Guid.NewGuid(), Name = "Prod", DatabaseType = DatabaseType.SqlServer };
        var filePath = WriteExportFile(new ConnectionExportFile
        {
            Connections = [new ConnectionExportEntry { Name = "Prod", DatabaseType = "SqlServer" }]
        });
        var settingsRepository = new FakeConnectionSettingsRepository([existing]);
        var service = new ConnectionImportService(settingsRepository);

        await service.ImportAsync(filePath);

        var imported = settingsRepository.LoadAll().Single(c => c.Id != existing.Id);
        Assert.Equal("Prod (2)", imported.Name);
    }

    [Fact]
    public async Task ExportThenImport_RoundTrips_ConnectionData_ButLeavesItUngrouped()
    {
        var group = new ConnectionGroup { Id = Guid.NewGuid(), Name = "Production" };
        var original = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            Name = "Prod",
            DatabaseType = DatabaseType.SqlServer,
            Server = "sql.local",
            Database = "master",
            Port = 1433,
            User = "sa"
        };
        var exportSettingsRepository = new FakeConnectionSettingsRepository([original]);
        var exportService = new ConnectionExportService(exportSettingsRepository);
        var filePath = Path.Combine(_tempDirectory, "roundtrip.json");

        await exportService.ExportAsync([original], filePath, includePasswords: false);

        var importSettingsRepository = new FakeConnectionSettingsRepository();
        var importService = new ConnectionImportService(importSettingsRepository);

        var importedCount = await importService.ImportAsync(filePath);

        Assert.Equal(1, importedCount);
        var imported = Assert.Single(importSettingsRepository.LoadAll());
        Assert.NotEqual(original.Id, imported.Id);
        Assert.Equal(original.Name, imported.Name);
        Assert.Null(imported.GroupId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }
}
