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
    public void ShouldTriggerCompletion_ReturnsTrue_ForWhitespaceSoValidContextsCanOpen()
    {
        Assert.True(SqlCompletionProvider.ShouldTriggerCompletion(" "));
    }

    [Fact]
    public void ShouldTriggerCompletion_ReturnsFalse_ForNewline()
    {
        Assert.False(SqlCompletionProvider.ShouldTriggerCompletion("\n"));
    }

    [Fact]
    public void ShouldTriggerCompletion_ReturnsFalse_ForCarriageReturn()
    {
        Assert.False(SqlCompletionProvider.ShouldTriggerCompletion("\r"));
    }

    [Fact]
    public void AutoRequest_AfterSelectNewline_ReturnsNull()
    {
        var sql = "select \n";

        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, "\n");

        Assert.Null(request);
    }

    [Fact]
    public void AutoRequest_AfterFromNewline_ReturnsNull()
    {
        var sql = "select * from \n";

        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, "\n");

        Assert.Null(request);
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
    public void ManualRequest_AfterGroupBy_ReturnsColumnsTrigger()
    {
        var sql = "select * from clientes group by ";

        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.GroupBy, request.Context.Clause);
    }

    [Fact]
    public void ManualRequest_AfterOrderBy_ReturnsColumnsTrigger()
    {
        var sql = "select * from clientes order by ";

        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.OrderBy, request.Context.Clause);
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
    public async Task GetCompletionsAsync_AfterSelectSpace_ReturnsColumnsFromSeededSchemaCache()
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.SqlServer };
        SeedSchemaCache(connection.Id, "clientes", "id", "nome");

        var sql = "select ";
        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, " ");

        var completions = await SqlCompletionProvider.GetCompletionsAsync(connection, sql, sql.Length, request!);

        Assert.NotNull(request);
        Assert.Contains(completions, item => item.Text == "id");
        Assert.Contains(completions, item => item.Text == "nome");
    }

    [Fact]
    public async Task GetCompletionsAsync_ForColumn_ShowsSourceAndDataTypeInDescription()
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.SqlServer };
        SeedSchemaCache(
            connection.Id,
            "temp",
            ("id", "int", 0, 0, 0),
            ("campo1", "varchar", 100, 0, 0),
            ("campo3", "decimal", 0, 10, 3));

        var sql = "select ";
        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

        var completions = await SqlCompletionProvider.GetCompletionsAsync(connection, sql, sql.Length, request);

        var id = Assert.IsType<SqlCompletionData>(completions.Single(item => item.Text == "id"));
        var campo1 = Assert.IsType<SqlCompletionData>(completions.Single(item => item.Text == "campo1"));
        var campo3 = Assert.IsType<SqlCompletionData>(completions.Single(item => item.Text == "campo3"));
        Assert.Equal("from temp int", id.Description);
        Assert.Equal("from temp varchar (100)", campo1.Description);
        Assert.Equal("from temp decimal(10, 3)", campo3.Description);
    }

    [Fact]
    public async Task GetCompletionsAsync_AfterSelectSpace_ReturnsProviderFunctions()
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.SqlServer };
        SeedSchemaCache(connection.Id, "clientes", "id", "nome");

        var sql = "select ";
        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

        var completions = await SqlCompletionProvider.GetCompletionsAsync(connection, sql, sql.Length, request);

        var getDate = Assert.IsType<SqlCompletionData>(completions.Single(item => item.Text == "GETDATE"));
        Assert.Equal(CompletionItemKind.Function, getDate.Kind);
        Assert.Equal("Returns the current database system timestamp.", getDate.Description);
        Assert.Equal("returns date/time", getDate.Detail);
        Assert.Contains(completions, item => item.Text == "SUM");
    }

    [Fact]
    public async Task GetCompletionsAsync_AfterSelectWord_FiltersProviderFunctions()
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.Oracle };
        SeedSchemaCache(connection.Id, "clientes", "id", "nome");

        var sql = "select nv";
        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

        var completions = await SqlCompletionProvider.GetCompletionsAsync(connection, sql, sql.Length, request);

        Assert.Contains(completions, item => item.Text == "NVL");
        Assert.Contains(completions, item => item.Text == "NVL2");
        Assert.DoesNotContain(completions, item => item.Text == "SYSDATE");
    }

    [Fact]
    public async Task GetCompletionsAsync_AfterFrom_DoesNotReturnFunctions()
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.SqlServer };
        SeedSchemaCache(connection.Id, "clientes", "id", "nome");

        var sql = "select * from ";
        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

        var completions = await SqlCompletionProvider.GetCompletionsAsync(connection, sql, sql.Length, request);

        Assert.Contains(completions, item => item.Text == "clientes");
        Assert.DoesNotContain(completions, item => item.Text == "GETDATE");
        Assert.DoesNotContain(completions.OfType<SqlCompletionData>(), item => item.Kind == CompletionItemKind.Function);
    }

    [Fact]
    public async Task GetCompletionsAsync_AfterAliasDot_DoesNotReturnFunctions()
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.PostgresSql };
        SeedSchemaCache(connection.Id, "clientes", "id", "nome");

        var sql = "select c. from clientes c";
        var caretOffset = "select c.".Length;
        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, caretOffset);

        var completions = await SqlCompletionProvider.GetCompletionsAsync(connection, sql, caretOffset, request);

        Assert.Contains(completions, item => item.Text == "id");
        Assert.DoesNotContain(completions, item => item.Text == "NOW");
        Assert.DoesNotContain(completions.OfType<SqlCompletionData>(), item => item.Kind == CompletionItemKind.Function);
    }

    [Fact]
    public async Task GetCompletionsAsync_AfterSelectSpaceWithFromTable_ReturnsColumnsOnlyFromReferencedTable()
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.SqlServer };
        SeedSchemaCache(
            connection.Id,
            ("clientes", ["id", "nome"]),
            ("pedidos", ["pedido_id", "valor"]));

        var sql = "select  from clientes";
        var caretOffset = "select ".Length;
        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, caretOffset);

        var completions = await SqlCompletionProvider.GetCompletionsAsync(connection, sql, caretOffset, request);

        Assert.Contains(completions, item => item.Text == "id");
        Assert.Contains(completions, item => item.Text == "nome");
        Assert.DoesNotContain(completions, item => item.Text == "pedido_id");
        Assert.DoesNotContain(completions, item => item.Text == "valor");
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

    [Fact]
    public async Task GetCompletionsAsync_ReturnsColumns_ForCteWithExplicitColumns()
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.SqlServer };
        SeedSchemaCache(connection.Id, "clientes", "id", "nome");

        var sql = """
                  with sales_cte (sale_id, customer_name) as (
                      select id, nome
                      from clientes
                  )
                  select s. from sales_cte s
                  """;
        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.IndexOf("select s.", StringComparison.Ordinal) + "select s.".Length);

        var completions = await SqlCompletionProvider.GetCompletionsAsync(connection, sql, sql.IndexOf("select s.", StringComparison.Ordinal) + "select s.".Length, request);

        Assert.Contains(completions, item => item.Text == "sale_id");
        Assert.Contains(completions, item => item.Text == "customer_name");
    }

    [Fact]
    public async Task GetCompletionsAsync_ReturnsColumns_ForCteWithInferredProjectionAliases()
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.SqlServer };
        SeedSchemaCache(connection.Id, "clientes", "id", "nome");

        var sql = """
                  with sales_cte as (
                      select c.id as sale_id, c.nome customer_name
                      from clientes c
                  )
                  select s. from sales_cte s
                  """;
        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.IndexOf("select s.", StringComparison.Ordinal) + "select s.".Length);

        var completions = await SqlCompletionProvider.GetCompletionsAsync(connection, sql, sql.IndexOf("select s.", StringComparison.Ordinal) + "select s.".Length, request);

        Assert.Contains(completions, item => item.Text == "sale_id");
        Assert.Contains(completions, item => item.Text == "customer_name");
    }

    private static void SeedSchemaCache(Guid connectionId, string tableName, params string[] columns)
    {
        SeedSchemaCache(connectionId, (tableName, columns.Select(column => (column, string.Empty, 0, 0, 0)).ToArray()));
    }

    private static void SeedSchemaCache(
        Guid connectionId,
        string tableName,
        params (string Name, string DataType, int Length, int Precision, int Scale)[] columns)
    {
        SeedSchemaCache(connectionId, (tableName, columns));
    }

    private static void SeedSchemaCache(Guid connectionId, params (string TableName, string[] Columns)[] tablesToSeed)
    {
        SeedSchemaCache(
            connectionId,
            tablesToSeed
                .Select(table => (
                    table.TableName,
                    table.Columns.Select(column => (column, string.Empty, 0, 0, 0)).ToArray()))
                .ToArray());
    }

    private static void SeedSchemaCache(
        Guid connectionId,
        params (string TableName, (string Name, string DataType, int Length, int Precision, int Scale)[] Columns)[] tablesToSeed)
    {
        var providerType = typeof(SqlCompletionProvider);
        var cacheField = providerType.GetField("SchemaCache", BindingFlags.Static | BindingFlags.NonPublic)
                         ?? throw new InvalidOperationException("Schema cache field not found.");
        var cacheDictionary = cacheField.GetValue(null) ?? throw new InvalidOperationException("Schema cache value not found.");

        var cacheType = providerType.GetNestedType("SchemaCompletionCache", BindingFlags.NonPublic)
                       ?? throw new InvalidOperationException("Schema completion cache type not found.");
        var columnInfoType = providerType.GetNestedType("ColumnCompletionInfo", BindingFlags.NonPublic)
                             ?? throw new InvalidOperationException("Column completion info type not found.");
        var cache = Activator.CreateInstance(cacheType) ?? throw new InvalidOperationException("Could not create schema cache.");

        cacheType.GetProperty("TablesLoaded")!.SetValue(cache, true);

        var tables = (ISet<string>)cacheType.GetProperty("Tables")!.GetValue(cache)!;
        var tableNodes = (IDictionary)cacheType.GetProperty("TableNodes")!.GetValue(cache)!;
        var columnsByTable = (IDictionary)cacheType.GetProperty("ColumnsByTable")!.GetValue(cache)!;
        var loadedTables = (ISet<string>)cacheType.GetProperty("LoadedTables")!.GetValue(cache)!;

        foreach (var (tableName, columns) in tablesToSeed)
        {
            tables.Add(tableName);

            var schemaNode = CreateSchemaNode(NodeType.Table, tableName);
            tableNodes[tableName] = schemaNode;
            tableNodes[$"[{tableName}]"] = schemaNode;
            tableNodes[$"`{tableName}`"] = schemaNode;
            tableNodes[$"\"{tableName}\""] = schemaNode;

            columnsByTable[tableName] = CreateColumnInfoArray(columnInfoType, columns);
            loadedTables.Add(tableName);
        }

        var tryAddMethod = cacheDictionary.GetType().GetMethod("TryAdd")
                          ?? throw new InvalidOperationException("Schema cache TryAdd method not found.");
        _ = tryAddMethod.Invoke(cacheDictionary, [connectionId, cache]);
    }

    private static Array CreateColumnInfoArray(
        Type columnInfoType,
        (string Name, string DataType, int Length, int Precision, int Scale)[] columns)
    {
        var array = Array.CreateInstance(columnInfoType, columns.Length);
        for (var index = 0; index < columns.Length; index++)
        {
            var column = columns[index];
            var columnInfo = Activator.CreateInstance(
                                 columnInfoType,
                                 column.Name,
                                 column.DataType,
                                 column.Length,
                                 column.Precision,
                                 column.Scale)
                             ?? throw new InvalidOperationException("Could not create column completion info.");
            array.SetValue(columnInfo, index);
        }

        return array;
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
        public Guid? GroupId { get; set; }
        public string Name { get; set; } = "Test";
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool Encrypt { get; set; }
        public bool TrustServerCertificate { get; set; }
        public bool AllowBlankPassword { get; set; }
        public int StatementTimeoutSeconds { get; set; } = ConnectionSettings.DefaultStatementTimeoutSeconds;
        public DmlTransactionMode DmlTransactionMode { get; set; } = DmlTransactionMode.AutoCommit;
        public DatabaseType DatabaseType { get; set; }
    }
}
