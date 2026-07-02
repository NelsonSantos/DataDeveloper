using System;
using System.Collections.Generic;
using System.IO;
using DataDeveloper.Core;
using DataDeveloper.Data.Models;
using DataDeveloper.Interfaces;
using Microsoft.Data.Sqlite;

namespace DataDeveloper.Services;

public class SqliteConnectionGroupRepository : IConnectionGroupRepository
{
    private const string DatabaseFolder = "state";
    private const string DatabaseFileName = "DataDeveloper.db";

    private readonly string _databasePath;
    private bool _isInitialized;

    public SqliteConnectionGroupRepository(AppDataFileService fileService)
        : this(fileService.GetFullPath(DatabaseFileName, DatabaseFolder))
    {
    }

    public SqliteConnectionGroupRepository(string databasePath)
    {
        _databasePath = databasePath;
    }

    public IReadOnlyList<ConnectionGroup> LoadAll()
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "select id, name from connection_group order by name;";

        using var reader = command.ExecuteReader();
        var groups = new List<ConnectionGroup>();
        while (reader.Read())
        {
            groups.Add(new ConnectionGroup
            {
                Id = Guid.Parse(reader.GetString(0)),
                Name = reader.GetString(1)
            });
        }

        return groups;
    }

    public void Save(ConnectionGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        EnsureInitialized();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              insert into connection_group (id, name)
                              values ($id, $name)
                              on conflict(id) do update set name = excluded.name;
                              """;

        var id = group.Id == Guid.Empty ? Guid.NewGuid() : group.Id;
        group.Id = id;
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$name", group.Name);
        command.ExecuteNonQuery();
    }

    public void Delete(Guid groupId)
    {
        EnsureInitialized();

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        // app_connection is owned by SqliteConnectionSettingsRepository; only touch it if it
        // has already been created, so group management works even before any connection does.
        if (TableExists(connection, transaction, "app_connection"))
        {
            using var unassignCommand = connection.CreateCommand();
            unassignCommand.Transaction = transaction;
            unassignCommand.CommandText = "update app_connection set group_id = null where group_id = $id;";
            unassignCommand.Parameters.AddWithValue("$id", groupId.ToString());
            unassignCommand.ExecuteNonQuery();
        }

        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "delete from connection_group where id = $id;";
            deleteCommand.Parameters.AddWithValue("$id", groupId.ToString());
            deleteCommand.ExecuteNonQuery();
        }

        transaction.Commit();
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
                              create table if not exists connection_group
                              (
                                  id text not null primary key,
                                  name text not null
                              );
                              """;
        command.ExecuteNonQuery();

        _isInitialized = true;
    }

    private static bool TableExists(SqliteConnection connection, SqliteTransaction transaction, string tableName)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select 1 from sqlite_master where type = 'table' and name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        return command.ExecuteScalar() is not null;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();
        return connection;
    }
}
