using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DataDeveloper.Core;
using DataDeveloper.Data.Ai;
using DataDeveloper.Interfaces;
using Microsoft.Data.Sqlite;

namespace DataDeveloper.Services;

public class SqliteAiChatHistoryRepository : IAiChatHistoryRepository
{
    private const string DatabaseFolder = "state";
    private const string DatabaseFileName = "DataDeveloper.db";

    private readonly string _databasePath;
    private bool _isInitialized;

    public SqliteAiChatHistoryRepository(AppDataFileService fileService)
        : this(fileService.GetFullPath(DatabaseFileName, DatabaseFolder))
    {
    }

    public SqliteAiChatHistoryRepository(string databasePath)
    {
        _databasePath = databasePath;
    }

    public IReadOnlyList<AiChatInfo> ListChats(Guid connectionId)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              select c.id, c.connection_id, c.title, c.model, c.created_at, c.updated_at,
                                     (select count(*) from ai_chat_message m where m.chat_id = c.id)
                              from ai_chat c
                              where c.connection_id = $connectionId
                              order by c.updated_at desc, c.created_at desc;
                              """;
        command.Parameters.AddWithValue("$connectionId", connectionId.ToString());

        var chats = new List<AiChatInfo>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            chats.Add(new AiChatInfo(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                ParseTime(reader.GetString(4)),
                ParseTime(reader.GetString(5)),
                reader.GetInt32(6)));
        }

        return chats;
    }

    public AiChatInfo CreateChat(Guid connectionId, string title = AiChatTitles.Default, string? model = null)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        return InsertChat(connection, connectionId, title, model, DateTime.UtcNow);
    }

    public void RenameChat(Guid chatId, string title)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "update ai_chat set title = $title where id = $id;";
        command.Parameters.AddWithValue("$title", NormalizeTitle(title));
        command.Parameters.AddWithValue("$id", chatId.ToString());
        command.ExecuteNonQuery();
    }

    public void SetChatModel(Guid chatId, string? model)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "update ai_chat set model = $model where id = $id;";
        command.Parameters.AddWithValue("$model", string.IsNullOrWhiteSpace(model) ? DBNull.Value : model.Trim());
        command.Parameters.AddWithValue("$id", chatId.ToString());
        command.ExecuteNonQuery();
    }

    public void DeleteChat(Guid chatId)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        Execute(connection, transaction, "delete from ai_chat_message where chat_id = $id;", ("$id", chatId.ToString()));
        Execute(connection, transaction, "delete from ai_chat where id = $id;", ("$id", chatId.ToString()));
        transaction.Commit();
    }

    public void DeleteConnectionChats(Guid connectionId)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        Execute(connection, transaction, "delete from ai_chat_message where connection_id = $id;", ("$id", connectionId.ToString()));
        Execute(connection, transaction, "delete from ai_chat where connection_id = $id;", ("$id", connectionId.ToString()));
        transaction.Commit();
    }

    public IReadOnlyList<AiChatMessage> LoadMessages(Guid chatId, int limit = 200)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              select role, content, created_at, model_content
                              from ai_chat_message
                              where chat_id = $chatId
                              order by id desc
                              limit $limit;
                              """;
        command.Parameters.AddWithValue("$chatId", chatId.ToString());
        command.Parameters.AddWithValue("$limit", limit);

        var messages = new List<AiChatMessage>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            messages.Add(new AiChatMessage(
                (AiChatRole)reader.GetInt32(0),
                reader.GetString(1),
                ParseTime(reader.GetString(2)),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        messages.Reverse();
        return messages;
    }

    public void Append(Guid chatId, AiChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        EnsureInitialized();

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        // The owning connection is copied from the chat so rows stay queryable by connection.
        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                                 insert into ai_chat_message (connection_id, chat_id, role, content, model_content, created_at)
                                 select connection_id, id, $role, $content, $modelContent, $createdAt
                                 from ai_chat
                                 where id = $chatId;
                                 """;
            insert.Parameters.AddWithValue("$chatId", chatId.ToString());
            insert.Parameters.AddWithValue("$role", (int)message.Role);
            insert.Parameters.AddWithValue("$content", message.Content);
            insert.Parameters.AddWithValue("$modelContent", (object?)message.ModelContent ?? DBNull.Value);
            insert.Parameters.AddWithValue("$createdAt", message.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            if (insert.ExecuteNonQuery() == 0)
                throw new InvalidOperationException($"The AI chat {chatId} does not exist.");
        }

        Execute(connection, transaction, "update ai_chat set updated_at = $updatedAt where id = $id;",
            ("$updatedAt", message.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)), ("$id", chatId.ToString()));
        transaction.Commit();
    }

    public void Clear(Guid chatId)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        Execute(connection, null, "delete from ai_chat_message where chat_id = $id;", ("$id", chatId.ToString()));
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
            return;

        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        Execute(connection, transaction, """
                                         create table if not exists ai_chat
                                         (
                                             id text primary key,
                                             connection_id text not null,
                                             title text not null,
                                             model text null,
                                             created_at text not null,
                                             updated_at text not null
                                         );
                                         create index if not exists ix_ai_chat_connection on ai_chat (connection_id, updated_at);
                                         create table if not exists ai_chat_message
                                         (
                                             id integer primary key autoincrement,
                                             connection_id text not null,
                                             role integer not null,
                                             content text not null,
                                             created_at text not null
                                         );
                                         create index if not exists ix_ai_chat_message_connection on ai_chat_message (connection_id, id);
                                         """);

        // Databases created before conversations existed have messages keyed by connection only.
        var columns = GetColumns(connection, transaction, "ai_chat_message");
        if (!columns.Contains("chat_id"))
            Execute(connection, transaction, "alter table ai_chat_message add column chat_id text null;");
        if (!columns.Contains("model_content"))
            Execute(connection, transaction, "alter table ai_chat_message add column model_content text null;");
        Execute(connection, transaction, "create index if not exists ix_ai_chat_message_chat on ai_chat_message (chat_id, id);");

        MigrateOrphanMessages(connection, transaction);

        transaction.Commit();
        _isInitialized = true;
    }

    // Each connection's old single history becomes its first conversation, so nobody loses a chat.
    private static void MigrateOrphanMessages(SqliteConnection connection, SqliteTransaction transaction)
    {
        var orphans = new List<(string ConnectionId, string FirstAt, string LastAt)>();
        using (var query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText = """
                                select connection_id, min(created_at), max(created_at)
                                from ai_chat_message
                                where chat_id is null
                                group by connection_id;
                                """;
            using var reader = query.ExecuteReader();
            while (reader.Read())
                orphans.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        foreach (var (connectionId, firstAt, lastAt) in orphans)
        {
            var chatId = Guid.NewGuid().ToString();
            Execute(connection, transaction, """
                                             insert into ai_chat (id, connection_id, title, model, created_at, updated_at)
                                             values ($id, $connectionId, $title, null, $createdAt, $updatedAt);
                                             """,
                ("$id", chatId), ("$connectionId", connectionId), ("$title", AiChatTitles.Migrated),
                ("$createdAt", firstAt), ("$updatedAt", lastAt));
            Execute(connection, transaction, "update ai_chat_message set chat_id = $chatId where chat_id is null and connection_id = $connectionId;",
                ("$chatId", chatId), ("$connectionId", connectionId));
        }
    }

    private static AiChatInfo InsertChat(SqliteConnection connection, Guid connectionId, string title, string? model, DateTime nowUtc)
    {
        var id = Guid.NewGuid();
        var normalizedModel = string.IsNullOrWhiteSpace(model) ? null : model.Trim();
        var normalizedTitle = NormalizeTitle(title);
        var stamp = nowUtc.ToString("O", CultureInfo.InvariantCulture);

        Execute(connection, null, """
                                  insert into ai_chat (id, connection_id, title, model, created_at, updated_at)
                                  values ($id, $connectionId, $title, $model, $createdAt, $updatedAt);
                                  """,
            ("$id", id.ToString()), ("$connectionId", connectionId.ToString()), ("$title", normalizedTitle),
            ("$model", (object?)normalizedModel ?? DBNull.Value), ("$createdAt", stamp), ("$updatedAt", stamp));

        return new AiChatInfo(id, connectionId, normalizedTitle, normalizedModel, nowUtc, nowUtc, 0);
    }

    private static string NormalizeTitle(string? title) =>
        string.IsNullOrWhiteSpace(title) ? AiChatTitles.Default : AiChatTitles.FromText(title);

    private static HashSet<string> GetColumns(SqliteConnection connection, SqliteTransaction transaction, string table)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"pragma table_info({table});";
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            columns.Add(reader.GetString(1));
        return columns;
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }

    private static DateTime ParseTime(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();
        return connection;
    }
}
