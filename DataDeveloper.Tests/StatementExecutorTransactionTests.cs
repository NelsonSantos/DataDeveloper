using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Data.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DataDeveloper.Tests;

public class StatementExecutorTransactionTests
{
    [Fact]
    public async Task ExecuteStatement_OpensTransactionForDmlUntilRollback()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            EnsureDatabaseServices();
            var settings = CreateSqLiteSettings(databasePath, DmlTransactionMode.ManualCommitRollback);
            var executor = settings.GetStatementExecutor();

            await ExecuteAndClose(executor, "create table items(id integer primary key, name text)");
            await ExecuteAndClose(executor, "insert into items(name) values ('pending')");

            Assert.True(executor.HasActiveTransaction);
            Assert.Equal(1L, await ExecuteScalar<long>(executor, "select count(*) from items"));

            await executor.RollbackTransaction();

            Assert.False(executor.HasActiveTransaction);
            Assert.Equal(0L, await ExecuteScalar<long>(executor, "select count(*) from items"));
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task CommitTransaction_PersistsPendingDml()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            EnsureDatabaseServices();
            var settings = CreateSqLiteSettings(databasePath, DmlTransactionMode.ManualCommitRollback);
            var executor = settings.GetStatementExecutor();

            await ExecuteAndClose(executor, "create table items(id integer primary key, name text)");
            await ExecuteAndClose(executor, "insert into items(name) values ('committed')");

            await executor.CommitTransaction();

            Assert.False(executor.HasActiveTransaction);
            Assert.Equal(1L, await ExecuteScalar<long>(settings.GetStatementExecutor(), "select count(*) from items"));
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ExecuteStatement_SupportsExplicitBeginTransactionAndCommitScript()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            EnsureDatabaseServices();
            var settings = CreateSqLiteSettings(databasePath);
            var executor = settings.GetStatementExecutor();

            await ExecuteAndClose(executor, "create table items(id integer primary key, name text)");
            await ExecuteAndClose(
                executor,
                """
                begin transaction;
                insert into items(name) values ('scripted');
                commit;
                """);

            Assert.False(executor.HasActiveTransaction);
            Assert.Equal(1L, await ExecuteScalar<long>(settings.GetStatementExecutor(), "select count(*) from items"));
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ExecuteStatement_AutoCommitMode_PersistsDmlWithoutPendingTransaction()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            EnsureDatabaseServices();
            var settings = CreateSqLiteSettings(databasePath, DmlTransactionMode.AutoCommit);
            var executor = settings.GetStatementExecutor();

            await ExecuteAndClose(executor, "create table items(id integer primary key, name text)");
            await ExecuteAndClose(executor, "insert into items(name) values ('auto')");

            Assert.False(executor.HasActiveTransaction);
            Assert.Equal(1L, await ExecuteScalar<long>(settings.GetStatementExecutor(), "select count(*) from items"));
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ExecuteCommandInTransaction_KeepsEditableGridCommandPendingUntilRollback()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            EnsureDatabaseServices();
            var settings = CreateSqLiteSettings(databasePath);
            var executor = settings.GetStatementExecutor();

            await ExecuteAndClose(executor, "create table items(id integer primary key, name text)");

            var command = new EditableResultSetCommand(
                "insert into items(name) values (@name);",
                new Dictionary<string, object?> { ["name"] = "grid" });
            var affectedRows = await executor.ExecuteCommandInTransaction(command);

            Assert.Equal(1, affectedRows);
            Assert.True(executor.HasActiveTransaction);
            Assert.Equal(1L, await ExecuteScalar<long>(executor, "select count(*) from items"));
            Assert.Equal(0L, await ExecuteScalar<long>(settings.GetStatementExecutor(), "select count(*) from items"));

            await executor.RollbackTransaction();

            Assert.False(executor.HasActiveTransaction);
            Assert.Equal(0L, await ExecuteScalar<long>(settings.GetStatementExecutor(), "select count(*) from items"));
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    private static async Task ExecuteAndClose(IStatementExecutor executor, string sql)
    {
        var results = await executor.ExecuteStatement(sql);
        foreach (var result in results)
            await result.CloseDataReader();
    }

    private static async Task<T?> ExecuteScalar<T>(IStatementExecutor executor, string sql)
    {
        var result = (await executor.ExecuteStatement(sql)).First();
        try
        {
            Assert.NotNull(result.DataReader);
            Assert.True(result.DataReader!.Read());
            return (T)Convert.ChangeType(result.DataReader.GetValue(0), typeof(T));
        }
        finally
        {
            await result.CloseDataReader();
        }
    }

    private static SqLiteConnectionSettings CreateSqLiteSettings(
        string databasePath,
        DmlTransactionMode dmlTransactionMode = DmlTransactionMode.AutoCommit)
    {
        return new SqLiteConnectionSettings
        {
            Id = Guid.NewGuid(),
            Name = "Transaction test",
            DatabaseType = DatabaseType.SqLite,
            DmlTransactionMode = dmlTransactionMode,
            Database = databasePath
        };
    }

    private static string CreateDatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), $"datadeveloper-transactions-{Guid.NewGuid():N}.db");
    }

    private static void EnsureDatabaseServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<DatabaseProviderFactoryService>();
        DatabaseExtensionsMethods.SetServiceProvider(services.BuildServiceProvider());
    }
}
