using DataDeveloper.Data.Ai;
using DataDeveloper.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DataDeveloper.Tests.Ai;

public class AiConnectionCleanupTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("ai-cleanup").FullName;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void Forget_RemovesChatsAndSessionFolderOfThatConnectionOnly()
    {
        var database = Path.Combine(_folder, "state.db");
        var history = new SqliteAiChatHistoryRepository(database);
        var factory = new AiProviderFactoryService(new NeverRunner(), Path.Combine(_folder, "sessions"));
        var cleanup = new AiConnectionCleanup(history, new SqliteAiSettingsRepository(database), factory);

        var gone = Guid.NewGuid();
        var kept = Guid.NewGuid();
        history.CreateChat(gone);
        history.CreateChat(gone);
        history.CreateChat(kept);
        var goneFolder = factory.GetSessionFolder(gone)!;
        var keptFolder = factory.GetSessionFolder(kept)!;
        Directory.CreateDirectory(goneFolder);
        File.WriteAllText(Path.Combine(goneFolder, "attachment.txt"), "x");
        Directory.CreateDirectory(keptFolder);

        cleanup.Forget(gone);

        Assert.Empty(history.ListChats(gone));
        Assert.Single(history.ListChats(kept));
        Assert.False(Directory.Exists(goneFolder));
        Assert.True(Directory.Exists(keptFolder));
    }

    [Fact]
    public void Forget_WithoutAnyFolderOrChat_DoesNotThrow()
    {
        var database = Path.Combine(_folder, "state.db");
        var factory = new AiProviderFactoryService(new NeverRunner(), Path.Combine(_folder, "sessions"));
        var cleanup = new AiConnectionCleanup(new SqliteAiChatHistoryRepository(database), new SqliteAiSettingsRepository(database), factory);

        cleanup.Forget(Guid.NewGuid());
    }

    private sealed class NeverRunner : ICliProcessRunner
    {
        public Task<CliProcessResult> RunAsync(CliProcessRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
