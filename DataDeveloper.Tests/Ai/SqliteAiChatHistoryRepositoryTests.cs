using DataDeveloper.Data.Ai;
using DataDeveloper.Interfaces;
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

    private string DatabasePath => Path.Combine(_folder, "test.db");

    private SqliteAiChatHistoryRepository CreateRepository() => new(DatabasePath);

    [Fact]
    public void Append_ThenLoad_ReturnsMessagesOldestFirstPerChat()
    {
        var repository = CreateRepository();
        var connectionId = Guid.NewGuid();
        var first = repository.CreateChat(connectionId);
        var second = repository.CreateChat(connectionId);
        var now = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

        repository.Append(first.Id, new AiChatMessage(AiChatRole.User, "q", now));
        repository.Append(first.Id, new AiChatMessage(AiChatRole.Assistant, "a", now.AddSeconds(1)));
        repository.Append(second.Id, new AiChatMessage(AiChatRole.User, "other", now));

        var loaded = CreateRepository().LoadMessages(first.Id);

        Assert.Equal(["q", "a"], loaded.Select(m => m.Content));
        Assert.Equal(AiChatRole.Assistant, loaded[1].Role);
        Assert.Equal(now, loaded[0].CreatedAtUtc);
        Assert.Single(repository.LoadMessages(second.Id));
    }

    [Fact]
    public void Append_KeepsTheModelContentThatIncludesAttachments()
    {
        var repository = CreateRepository();
        var chat = repository.CreateChat(Guid.NewGuid());

        repository.Append(chat.Id, new AiChatMessage(AiChatRole.User, "fix this\n\n(Attached: q.sql)", DateTime.UtcNow, "fix this\n\nAttached \"q.sql\": select 1"));
        repository.Append(chat.Id, new AiChatMessage(AiChatRole.Assistant, "done", DateTime.UtcNow));

        var loaded = repository.LoadMessages(chat.Id);

        Assert.Equal("fix this\n\nAttached \"q.sql\": select 1", loaded[0].ModelContent);
        Assert.Null(loaded[1].ModelContent);
    }

    [Fact]
    public void Append_ToAnUnknownChat_Throws()
    {
        var repository = CreateRepository();

        Assert.Throws<InvalidOperationException>(() =>
            repository.Append(Guid.NewGuid(), new AiChatMessage(AiChatRole.User, "x", DateTime.UtcNow)));
    }

    [Fact]
    public void LoadMessages_ReturnsOnlyTheLatestWhenOverTheLimit()
    {
        var repository = CreateRepository();
        var chat = repository.CreateChat(Guid.NewGuid());
        for (var i = 1; i <= 5; i++)
            repository.Append(chat.Id, new AiChatMessage(AiChatRole.User, $"m{i}", DateTime.UtcNow));

        Assert.Equal(["m4", "m5"], repository.LoadMessages(chat.Id, 2).Select(m => m.Content));
    }

    [Fact]
    public void ListChats_ReturnsTheMostRecentlyActiveFirstWithMessageCounts()
    {
        var repository = CreateRepository();
        var connectionId = Guid.NewGuid();
        var older = repository.CreateChat(connectionId, "Task A");
        var newer = repository.CreateChat(connectionId, "Task B");
        var other = repository.CreateChat(Guid.NewGuid(), "Elsewhere");
        var later = DateTime.UtcNow.AddMinutes(5);

        repository.Append(older.Id, new AiChatMessage(AiChatRole.User, "hi", later));

        var chats = repository.ListChats(connectionId);

        Assert.Equal(["Task A", "Task B"], chats.Select(c => c.Title));
        Assert.Equal([1, 0], chats.Select(c => c.MessageCount));
        Assert.DoesNotContain(chats, c => c.Id == other.Id);
        Assert.Equal(newer.Id, chats[1].Id);
    }

    [Fact]
    public void RenameChat_AndSetChatModel_ArePersisted()
    {
        var repository = CreateRepository();
        var connectionId = Guid.NewGuid();
        var chat = repository.CreateChat(connectionId);

        repository.RenameChat(chat.Id, "  Task A: orders  ");
        repository.SetChatModel(chat.Id, " sonnet ");

        var stored = CreateRepository().ListChats(connectionId).Single();
        Assert.Equal("Task A: orders", stored.Title);
        Assert.Equal("sonnet", stored.Model);

        repository.SetChatModel(chat.Id, " ");
        Assert.Null(repository.ListChats(connectionId).Single().Model);
    }

    [Fact]
    public void RenameChat_WithABlankTitle_FallsBackToTheDefault()
    {
        var repository = CreateRepository();
        var connectionId = Guid.NewGuid();
        var chat = repository.CreateChat(connectionId, "Task");

        repository.RenameChat(chat.Id, "   ");

        Assert.Equal(AiChatTitles.Default, repository.ListChats(connectionId).Single().Title);
    }

    [Fact]
    public void Clear_RemovesTheMessagesButKeepsTheChat()
    {
        var repository = CreateRepository();
        var connectionId = Guid.NewGuid();
        var first = repository.CreateChat(connectionId);
        var second = repository.CreateChat(connectionId);
        repository.Append(first.Id, new AiChatMessage(AiChatRole.User, "x", DateTime.UtcNow));
        repository.Append(second.Id, new AiChatMessage(AiChatRole.User, "y", DateTime.UtcNow));

        repository.Clear(first.Id);

        Assert.Empty(repository.LoadMessages(first.Id));
        Assert.Single(repository.LoadMessages(second.Id));
        Assert.Equal(2, repository.ListChats(connectionId).Count);
    }

    [Fact]
    public void DeleteChat_RemovesTheChatAndItsMessagesOnly()
    {
        var repository = CreateRepository();
        var connectionId = Guid.NewGuid();
        var first = repository.CreateChat(connectionId);
        var second = repository.CreateChat(connectionId);
        repository.Append(first.Id, new AiChatMessage(AiChatRole.User, "x", DateTime.UtcNow));
        repository.Append(second.Id, new AiChatMessage(AiChatRole.User, "y", DateTime.UtcNow));

        repository.DeleteChat(first.Id);

        Assert.Equal(second.Id, repository.ListChats(connectionId).Single().Id);
        Assert.Empty(repository.LoadMessages(first.Id));
        Assert.Single(repository.LoadMessages(second.Id));
    }

    [Fact]
    public void DeleteConnectionChats_RemovesEveryChatOfThatConnectionOnly()
    {
        var repository = CreateRepository();
        var connectionId = Guid.NewGuid();
        var otherConnectionId = Guid.NewGuid();
        var chat = repository.CreateChat(connectionId);
        var kept = repository.CreateChat(otherConnectionId);
        repository.Append(chat.Id, new AiChatMessage(AiChatRole.User, "x", DateTime.UtcNow));
        repository.Append(kept.Id, new AiChatMessage(AiChatRole.User, "y", DateTime.UtcNow));

        repository.DeleteConnectionChats(connectionId);

        Assert.Empty(repository.ListChats(connectionId));
        Assert.Empty(repository.LoadMessages(chat.Id));
        Assert.Single(repository.LoadMessages(kept.Id));
    }

    [Fact]
    public void Initialization_MigratesAnOldHistoryIntoOneChatPerConnection()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var start = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
        CreateLegacyDatabase(
            (first, 1, "old question", start),
            (first, 2, "old answer", start.AddMinutes(1)),
            (second, 1, "another", start.AddDays(1)));

        var repository = CreateRepository();

        var firstChat = Assert.Single(repository.ListChats(first));
        Assert.Equal(AiChatTitles.Migrated, firstChat.Title);
        Assert.Equal(2, firstChat.MessageCount);
        Assert.Equal(start.AddMinutes(1), firstChat.UpdatedAtUtc);
        Assert.Equal(["old question", "old answer"], repository.LoadMessages(firstChat.Id).Select(m => m.Content));
        Assert.Single(repository.LoadMessages(Assert.Single(repository.ListChats(second)).Id));
    }

    [Fact]
    public void Initialization_RunsTwiceWithoutDuplicatingMigratedChats()
    {
        var connectionId = Guid.NewGuid();
        CreateLegacyDatabase((connectionId, 1, "old", DateTime.UtcNow));

        _ = CreateRepository().ListChats(connectionId);

        Assert.Single(CreateRepository().ListChats(connectionId));
    }

    private void CreateLegacyDatabase(params (Guid ConnectionId, int Role, string Content, DateTime CreatedAt)[] rows)
    {
        using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();

        using (var create = connection.CreateCommand())
        {
            create.CommandText = """
                                 create table ai_chat_message
                                 (
                                     id integer primary key autoincrement,
                                     connection_id text not null,
                                     role integer not null,
                                     content text not null,
                                     created_at text not null
                                 );
                                 """;
            create.ExecuteNonQuery();
        }

        foreach (var row in rows)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = "insert into ai_chat_message (connection_id, role, content, created_at) values ($c, $r, $t, $d);";
            insert.Parameters.AddWithValue("$c", row.ConnectionId.ToString());
            insert.Parameters.AddWithValue("$r", row.Role);
            insert.Parameters.AddWithValue("$t", row.Content);
            insert.Parameters.AddWithValue("$d", row.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            insert.ExecuteNonQuery();
        }
    }
}

public class AiChatTitlesTests
{
    [Theory]
    [InlineData("", "New chat")]
    [InlineData("   \n  ", "New chat")]
    [InlineData("how do I join orders and customers?", "how do I join orders and customers?")]
    [InlineData("first line\nsecond line", "first line")]
    public void FromText_UsesTheFirstLineOrTheDefault(string text, string expected) =>
        Assert.Equal(expected, AiChatTitles.FromText(text));

    [Fact]
    public void FromText_ShortensLongLines()
    {
        var title = AiChatTitles.FromText(new string('a', 200));

        Assert.Equal(AiChatTitles.MaxLength, title.Length);
        Assert.EndsWith("…", title);
    }
}
