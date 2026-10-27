using DataDeveloper.Data.Ai;
using DataDeveloper.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DataDeveloper.Tests.Ai;

public class SqliteAiSettingsRepositoryTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("ai-settings").FullName;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    private SqliteAiSettingsRepository CreateRepository() => new(Path.Combine(_folder, "test.db"));

    [Fact]
    public void Load_ReturnsDefaultsWhenNothingWasSaved()
    {
        var settings = CreateRepository().Load(AiProviderType.ClaudeCode);

        Assert.Null(settings.ExecutablePath);
        Assert.Null(settings.Model);
        Assert.Equal(AiProviderSettings.DefaultTimeoutSeconds, settings.TimeoutSeconds);
    }

    [Fact]
    public void Save_RoundTripsAndOverwrites()
    {
        var repository = CreateRepository();
        repository.Save(new AiProviderSettings { ExecutablePath = "/opt/claude", Model = "sonnet", TimeoutSeconds = 45 });
        repository.Save(new AiProviderSettings { ExecutablePath = "/opt/other", Model = "  ", TimeoutSeconds = 5 });

        var loaded = CreateRepository().Load(AiProviderType.ClaudeCode);

        Assert.Equal("/opt/other", loaded.ExecutablePath);
        Assert.Null(loaded.Model);
        Assert.Equal(AiProviderSettings.MinTimeoutSeconds, loaded.TimeoutSeconds);
    }

    [Fact]
    public void Factory_CreatesClaudeCodeProvider()
    {
        var provider = new AiProviderFactoryService(new CliProcessRunner()).Create(new AiProviderSettings());

        Assert.Equal(AiProviderType.ClaudeCode, provider.ProviderType);
        Assert.Equal("Claude Code", provider.DisplayName);
    }
}
