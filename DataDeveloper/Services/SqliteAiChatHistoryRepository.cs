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

    public IReadOnlyList<AiChatMessage> Load(Guid connectionId, int limit = 200)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              select role, content, created_at
                              from ai_chat_message
                              where connection_id = $connectionId
                              order by id desc
                              limit $limit;
                              """;
        command.Parameters.AddWithValue("$connectionId", connectionId.ToString());
        command.Parameters.AddWithValue("$limit", limit);

        var messages = new List<AiChatMessage>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            messages.Add(new AiChatMessage(
                (AiChatRole)reader.GetInt32(0),
                reader.GetString(1),
                DateTime.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        }

        messages.Reverse();
        return messages;
    }

    public void Append(Guid connectionId, AiChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              insert into ai_chat_message (connection_id, role, content, created_at)
                              values ($connectionId, $role, $content, $createdAt);
                              """;
        command.Parameters.AddWithValue("$connectionId", connectionId.ToString());
        command.Parameters.AddWithValue("$role", (int)message.Role);
        command.Parameters.AddWithValue("$content", message.Content);
        command.Parameters.AddWithValue("$createdAt", message.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public void Clear(Guid connectionId)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "delete from ai_chat_message where connection_id = $connectionId;";
        command.Parameters.AddWithValue("$connectionId", connectionId.ToString());
        command.ExecuteNonQuery();
    }

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
                              create table if not exists ai_chat_message
                              (
                                  id integer primary key autoincrement,
                                  connection_id text not null,
                                  role integer not null,
                                  content text not null,
                                  created_at text not null
                              );
                              create index if not exists ix_ai_chat_message_connection on ai_chat_message (connection_id, id);
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
