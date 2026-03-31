using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class SqlCompletionProviderTests
{
    [Fact]
    public void AutoRequest_AfterFrom_ReturnsObjectsTrigger()
    {
        var sql = "select * from ";

        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, "m");

        Assert.NotNull(request);
        Assert.Equal(CompletionTrigger.Objects, request!.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.From, request.Context.Clause);
    }

    [Fact]
    public void AutoRequest_AfterSelectSpace_ReturnsColumnsTrigger()
    {
        var sql = "select ";

        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, " ");

        Assert.NotNull(request);
        Assert.Equal(CompletionTrigger.Columns, request!.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.Select, request.Context.Clause);
    }

    [Fact]
    public void AutoRequest_AfterFromSpace_ReturnsObjectsTrigger()
    {
        var sql = "select * from ";

        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, " ");

        Assert.NotNull(request);
        Assert.Equal(CompletionTrigger.Objects, request!.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.From, request.Context.Clause);
    }

    [Fact]
    public void ManualRequest_AfterSelect_ReturnsColumnsTrigger()
    {
        var sql = "select ";

        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.Select, request.Context.Clause);
    }

    [Fact]
    public void AutoRequest_InsertColumnList_OpenParen_ReturnsColumnsTrigger()
    {
        var sql = "insert into clientes (";

        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, "(");

        Assert.NotNull(request);
        Assert.True(request!.Context.IsInsideInsertColumnList);
        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
    }

    [Fact]
    public void AutoRequest_InsertValuesOpenParen_DoesNotReturnColumnsTrigger()
    {
        var sql = "insert into clientes (id, nome) values (";

        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, "(");

        Assert.Null(request);
    }

    [Fact]
    public void AutoRequest_UpdateSetComma_ReturnsColumnsTrigger()
    {
        var sql = "update clientes set nome = @nome,";

        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, ",");

        Assert.NotNull(request);
        Assert.True(request!.Context.IsInsideUpdateSetList);
        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.Set, request.Context.Clause);
    }

    [Fact]
    public void AutoRequest_AfterJoin_ReturnsObjectsTrigger()
    {
        var sql = "select * from clientes c inner join ";

        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, "p");

        Assert.NotNull(request);
        Assert.Equal(CompletionTrigger.Objects, request!.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.Join, request.Context.Clause);
    }

    [Fact]
    public void ManualRequest_AfterDeleteWhere_ReturnsColumnsTrigger()
    {
        var sql = "delete from clientes where ";

        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.Where, request.Context.Clause);
    }

    [Fact]
    public void ManualRequest_AfterAliasDot_ReturnsColumnsTrigger()
    {
        var sql = "select c. from clientes c";
        var caretOffset = "select c.".Length;

        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, caretOffset);

        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal("c", request.Context.ObjectNameBeforeDot);
    }

    [Fact]
    public void AutoRequest_InsertColumnListCommaWithSpace_KeepsColumnsTrigger()
    {
        var sql = "insert into clientes (id, ";

        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

        Assert.True(request.Context.IsInsideInsertColumnList);
        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.Into, request.Context.Clause);
    }

    [Fact]
    public void ManualRequest_AfterMySqlQuotedAliasDot_ReturnsColumnsTrigger()
    {
        var sql = "select `c`.";
        var caretOffset = sql.Length;

        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, caretOffset);

        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal("c", request.Context.ObjectNameBeforeDot);
    }

    [Fact]
    public void AutoRequest_InsertIntoMySqlQuotedTable_OpenParen_ReturnsColumnsTrigger()
    {
        var sql = "insert into `clientes` (";

        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, "(");

        Assert.NotNull(request);
        Assert.True(request!.Context.IsInsideInsertColumnList);
        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal("clientes", request.Context.TargetTableName);
    }

    [Fact]
    public void ManualRequest_AfterMySqlQuotedUpdateSet_ReturnsColumnsTrigger()
    {
        var sql = "update `clientes` set ";

        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.Set, request.Context.Clause);
        Assert.Equal("clientes", request.Context.TargetTableName);
    }

    [Fact]
    public void ManualRequest_AfterPostgresQuotedAliasDot_ReturnsColumnsTrigger()
    {
        var sql = "select \"c\".";
        var caretOffset = sql.Length;

        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, caretOffset);

        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal("c", request.Context.ObjectNameBeforeDot);
    }

    [Fact]
    public void AutoRequest_InsertIntoPostgresQuotedTable_OpenParen_ReturnsColumnsTrigger()
    {
        var sql = "insert into \"clientes\" (";

        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, "(");

        Assert.NotNull(request);
        Assert.True(request!.Context.IsInsideInsertColumnList);
        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal("clientes", request.Context.TargetTableName);
    }

    [Fact]
    public void AutoRequest_InsertColumnList_AfterPreviousSelect_DoesNotThrowAndReturnsColumnsTrigger()
    {
        var sql = "select * from clientes;\ninsert into pedidos (";

        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, "(");

        Assert.NotNull(request);
        Assert.True(request!.Context.IsInsideInsertColumnList);
        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal("pedidos", request.Context.TargetTableName);
    }

    [Fact]
    public void ManualRequest_AfterPostgresQuotedUpdateSet_ReturnsColumnsTrigger()
    {
        var sql = "update \"clientes\" set ";

        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.Set, request.Context.Clause);
        Assert.Equal("clientes", request.Context.TargetTableName);
    }

    [Fact]
    public async Task GetCompletionsAsync_ReturnsColumns_ForSqlServerAliasWithSeededSchemaCache()
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.SqlServer };
        SeedSchemaCache(connection.Id, "clientes", "id", "nome");

        var sql = "select c. from clientes c";
        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, "select c.".Length);

        var completions = await SqlCompletionProvider.GetCompletionsAsync(connection, sql, "select c.".Length, request);

        Assert.Contains(completions, item => item.Text == "id");
        Assert.Contains(completions, item => item.Text == "nome");
    }

    [Fact]
    public async Task GetCompletionsAsync_ReturnsColumns_ForMySqlQuotedAliasWithSeededSchemaCache()
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.MySql };
        SeedSchemaCache(connection.Id, "clientes", "id", "nome");

        var sql = "select `c`. from `clientes` as `c`";
        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, "select `c`.".Length);

        var completions = await SqlCompletionProvider.GetCompletionsAsync(connection, sql, "select `c`.".Length, request);

        Assert.Contains(completions, item => item.Text == "id");
        Assert.Contains(completions, item => item.Text == "nome");
    }

    [Fact]
    public async Task GetCompletionsAsync_ReturnsColumns_ForPostgresQuotedAliasWithSeededSchemaCache()
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.PostgresSql };
        SeedSchemaCache(connection.Id, "clientes", "id", "nome");

        var sql = "select \"c\". from \"clientes\" as \"c\"";
        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, "select \"c\".".Length);

        var completions = await SqlCompletionProvider.GetCompletionsAsync(connection, sql, "select \"c\".".Length, request);

        Assert.Contains(completions, item => item.Text == "id");
        Assert.Contains(completions, item => item.Text == "nome");
    }

    private static void SeedSchemaCache(Guid connectionId, string tableName, params string[] columns)
    {
        var providerType = typeof(SqlCompletionProvider);
        var cacheField = providerType.GetField("SchemaCache", BindingFlags.Static | BindingFlags.NonPublic)
                         ?? throw new InvalidOperationException("Schema cache field not found.");
        var cacheDictionary = cacheField.GetValue(null) ?? throw new InvalidOperationException("Schema cache value not found.");

        var cacheType = providerType.GetNestedType("SchemaCompletionCache", BindingFlags.NonPublic)
                       ?? throw new InvalidOperationException("Schema completion cache type not found.");
        var cache = Activator.CreateInstance(cacheType) ?? throw new InvalidOperationException("Could not create schema cache.");

        cacheType.GetProperty("TablesLoaded")!.SetValue(cache, true);

        var tables = (ISet<string>)cacheType.GetProperty("Tables")!.GetValue(cache)!;
        tables.Add(tableName);

        var tableNodes = (IDictionary)cacheType.GetProperty("TableNodes")!.GetValue(cache)!;
        var schemaNode = CreateSchemaNode(NodeType.Table, tableName);
        tableNodes[tableName] = schemaNode;
        tableNodes[$"[{tableName}]"] = schemaNode;
        tableNodes[$"`{tableName}`"] = schemaNode;
        tableNodes[$"\"{tableName}\""] = schemaNode;

        var columnsByTable = (IDictionary)cacheType.GetProperty("ColumnsByTable")!.GetValue(cache)!;
        columnsByTable[tableName] = columns;

        var loadedTables = (ISet<string>)cacheType.GetProperty("LoadedTables")!.GetValue(cache)!;
        loadedTables.Add(tableName);

        var tryAddMethod = cacheDictionary.GetType().GetMethod("TryAdd")
                          ?? throw new InvalidOperationException("Schema cache TryAdd method not found.");
        _ = tryAddMethod.Invoke(cacheDictionary, [connectionId, cache]);
    }

    private static SchemaNode CreateSchemaNode(NodeType nodeType, string name)
    {
        var ctor = typeof(SchemaNode).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(NodeType), typeof(string), typeof(bool), typeof(SchemaNode), typeof(bool), typeof(string), typeof(object)],
            modifiers: null);

        return (SchemaNode)(ctor?.Invoke([nodeType, name, false, null!, false, null!, null!])
               ?? throw new InvalidOperationException("SchemaNode constructor not found."));
    }

    private sealed class TestConnectionSettings : IConnectionSettings
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Test";
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool Encrypt { get; set; }
        public bool TrustServerCertificate { get; set; }
        public bool AllowBlankPassword { get; set; }
        public DatabaseType DatabaseType { get; set; }
    }
}
