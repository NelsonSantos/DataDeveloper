using DataDeveloper.Data.Ai;
using DataDeveloper.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DataDeveloper.Tests.Ai;

public class SqliteAiChatHistoryRepositoryTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("ai-history").FullName;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    private SqliteAiChatHistoryRepository CreateRepository() => new(Path.Combine(_folder, "test.db"));

    [Fact]
    public void Append_ThenLoad_ReturnsMessagesOldestFirstPerConnection()
    {
        var repository = CreateRepository();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var now = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

        repository.Append(first, new AiChatMessage(AiChatRole.User, "q", now));
        repository.Append(first, new AiChatMessage(AiChatRole.Assistant, "a", now.AddSeconds(1)));
        repository.Append(second, new AiChatMessage(AiChatRole.User, "other", now));

        var loaded = CreateRepository().Load(first);

        Assert.Equal(["q", "a"], loaded.Select(m => m.Content));
        Assert.Equal(AiChatRole.Assistant, loaded[1].Role);
        Assert.Equal(now, loaded[0].CreatedAtUtc);
        Assert.Single(repository.Load(second));
    }

    [Fact]
    public void Load_ReturnsOnlyTheLatestWhenOverTheLimit()
    {
        var repository = CreateRepository();
        var id = Guid.NewGuid();
        for (var i = 1; i <= 5; i++)
            repository.Append(id, new AiChatMessage(AiChatRole.User, $"m{i}", DateTime.UtcNow));

        Assert.Equal(["m4", "m5"], repository.Load(id, 2).Select(m => m.Content));
    }

    [Fact]
    public void Clear_RemovesOnlyThatConnectionsMessages()
    {
        var repository = CreateRepository();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        repository.Append(first, new AiChatMessage(AiChatRole.User, "x", DateTime.UtcNow));
        repository.Append(second, new AiChatMessage(AiChatRole.User, "y", DateTime.UtcNow));

        repository.Clear(first);

        Assert.Empty(repository.Load(first));
        Assert.Single(repository.Load(second));
    }
}
