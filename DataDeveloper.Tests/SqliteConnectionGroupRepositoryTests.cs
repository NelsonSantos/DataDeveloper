using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class SqliteConnectionGroupRepositoryTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _databasePath;

    public SqliteConnectionGroupRepositoryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DataDeveloperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "DataDeveloper.db");
    }

    [Fact]
    public void Save_AndLoadAll_RoundTripsGroup()
    {
        var repository = new SqliteConnectionGroupRepository(_databasePath);
        var group = new ConnectionGroup { Id = Guid.NewGuid(), Name = "Production" };

        repository.Save(group);
        var loaded = Assert.Single(repository.LoadAll());

        Assert.Equal(group.Id, loaded.Id);
        Assert.Equal("Production", loaded.Name);
    }

    [Fact]
    public void Save_WithSameId_RenamesExistingGroup()
    {
        var repository = new SqliteConnectionGroupRepository(_databasePath);
        var group = new ConnectionGroup { Id = Guid.NewGuid(), Name = "Dev" };
        repository.Save(group);

        group.Name = "Development";
        repository.Save(group);

        var loaded = Assert.Single(repository.LoadAll());
        Assert.Equal("Development", loaded.Name);
    }

    [Fact]
    public void Delete_RemovesGroup_AndUngroupsConnectionsReferencingIt()
    {
        var groupRepository = new SqliteConnectionGroupRepository(_databasePath);
        var connectionRepository = new SqliteConnectionSettingsRepository(_databasePath, new InMemorySecretStore());

        var group = new ConnectionGroup { Id = Guid.NewGuid(), Name = "Production" };
        groupRepository.Save(group);

        var connection = new SqlServerConnectionSettings
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            Name = "Prod SQL",
            DatabaseType = DatabaseType.SqlServer,
            Server = "sql.local",
            Database = "master",
            User = "sa",
            Password = "secret"
        };
        connectionRepository.Save(connection);

        groupRepository.Delete(group.Id);

        Assert.Empty(groupRepository.LoadAll());
        var reloaded = Assert.Single(connectionRepository.LoadAll());
        Assert.Null(reloaded.GroupId);
    }

    [Fact]
    public void Delete_WhenNoConnectionsTableExistsYet_DoesNotThrow()
    {
        var groupRepository = new SqliteConnectionGroupRepository(_databasePath);
        var group = new ConnectionGroup { Id = Guid.NewGuid(), Name = "Production" };
        groupRepository.Save(group);

        groupRepository.Delete(group.Id);

        Assert.Empty(groupRepository.LoadAll());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }
}
