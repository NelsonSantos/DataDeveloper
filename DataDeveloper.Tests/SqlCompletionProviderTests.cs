using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Data.Services;
using DataDeveloper.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
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

    [Theory]
    [InlineData("select next value for ", " ")]
    [InlineData("select next value for ord", "d")]
    [InlineData("insert into t (id) values (next value for dbo.", ".")]
    [InlineData("select nextval('", "'")]
    [InlineData("select setval('ord", "d")]
    public void GetAutoCompletionRequest_InSequenceContexts_RequestsSequences(string sql, string insertedText)
    {
        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, insertedText);

        Assert.NotNull(request);
        Assert.Equal(CompletionTrigger.Sequences, request!.Context.Trigger);
    }

    [Theory]
    [InlineData("select * from t where name = '")]
    [InlineData("insert into t values ('")]
    public void GetAutoCompletionRequest_ForAnOrdinaryQuote_DoesNotOpen(string sql)
    {
        Assert.Null(SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, "'"));
    }

    [Fact]
    public async Task GetCompletionsAsync_InSequenceContext_ReturnsOnlySequences()
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.SqlServer };
        SeedObjects(connection.Id, ("clientes", NodeType.Table, ["id"]));
        SeedSequences(connection.Id, "order_number", "invoice_number");

        var sql = "select next value for ord";
        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

        var completions = (await SqlCompletionProvider.GetCompletionsAsync(connection, sql, sql.Length, request)).OfType<SqlCompletionData>().ToList();

        var sequence = Assert.Single(completions);
        Assert.Equal("order_number", sequence.Text);
        Assert.Equal(CompletionItemKind.Sequence, sequence.Kind);
    }

    [Fact]
    public async Task GetCompletionsAsync_AfterFrom_ListsTablesViewsAndSynonyms()
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.SqlServer };
        SeedObjects(
            connection.Id,
            ("clientes", NodeType.Table, ["id"]),
            ("clientes_ativos", NodeType.View, ["id"]),
            ("cli", NodeType.Synonym, ["id"]));
        SeedSequences(connection.Id, "cli_seq");

        var sql = "select * from cli";
        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

        var completions = (await SqlCompletionProvider.GetCompletionsAsync(connection, sql, sql.Length, request)).OfType<SqlCompletionData>().ToList();

        Assert.Equal(CompletionItemKind.Table, Assert.Single(completions, item => item.Text == "clientes").Kind);
        var view = Assert.Single(completions, item => item.Text == "clientes_ativos");
        Assert.Equal((CompletionItemKind.View, "View"), (view.Kind, view.Description));
        var synonym = Assert.Single(completions, item => item.Text == "cli");
        Assert.Equal((CompletionItemKind.Synonym, "Synonym"), (synonym.Kind, synonym.Description));
        Assert.DoesNotContain(completions, item => item.Text == "cli_seq");
    }

    [Theory]
    [InlineData("clientes_ativos", NodeType.View)]
    [InlineData("cli", NodeType.Synonym)]
    public async Task GetCompletionsAsync_AfterAliasDot_ReturnsColumnsOfViewsAndSynonyms(string objectName, NodeType nodeType)
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.SqlServer };
        SeedObjects(connection.Id, (objectName, nodeType, ["id", "nome"]));

        var sql = $"select x. from {objectName} x";
        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, "select x.".Length);

        var completions = await SqlCompletionProvider.GetCompletionsAsync(connection, sql, "select x.".Length, request);

        Assert.Contains(completions, item => item.Text == "id");
        Assert.Contains(completions, item => item.Text == "nome");
    }

    [Fact]
    public async Task GetCompletionsAsync_WithoutReferencedSource_FallsBackToTablesOnly()
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.SqlServer };
        SeedObjects(
            connection.Id,
            ("clientes", NodeType.Table, ["nome"]),
            ("clientes_ativos", NodeType.View, ["coluna_da_view"]));

        var sql = "select ";
        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

        var completions = await SqlCompletionProvider.GetCompletionsAsync(connection, sql, sql.Length, request);

        Assert.Contains(completions, item => item.Text == "nome");
        Assert.DoesNotContain(completions, item => item.Text == "coluna_da_view");
    }

    [Fact]
    public async Task GetCompletionsAsync_OracleSequenceDot_ReturnsNextvalAndCurrval()
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.Oracle };
        SeedObjects(connection.Id, ("CLIENTES", NodeType.Table, ["ID"]));
        SeedSequences(connection.Id, "ORDER_SEQ");

        var sql = "select order_seq.";
        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, ".");

        var completions = (await SqlCompletionProvider.GetCompletionsAsync(connection, sql, sql.Length, request!)).OfType<SqlCompletionData>().ToList();

        Assert.Equal(["CURRVAL", "NEXTVAL"], completions.Select(item => item.Text).OrderBy(text => text));
        Assert.All(completions, item => Assert.Equal(CompletionItemKind.Sequence, item.Kind));
    }

    [Fact]
    public async Task GetCompletionsAsync_SequenceDotOutsideOracle_DoesNotSuggestPseudocolumns()
    {
        var connection = new TestConnectionSettings { DatabaseType = DatabaseType.PostgresSql };
        SeedSequences(connection.Id, "order_seq");

        var sql = "select order_seq.";
        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, ".");

        var completions = await SqlCompletionProvider.GetCompletionsAsync(connection, sql, sql.Length, request!);

        Assert.DoesNotContain(completions, item => item.Text == "NEXTVAL");
    }

    [Fact]
    public async Task GetCompletionsAsync_OnSqlite_ListsViewsAndTheirColumns()
    {
        var services = new ServiceCollection();
        services.AddSingleton<DatabaseProviderFactoryService>();
        DatabaseExtensionsMethods.SetServiceProvider(services.BuildServiceProvider());

        var databasePath = Path.Combine(Path.GetTempPath(), $"datadeveloper-completion-{Guid.NewGuid():N}.db");
        try
        {
            using (var setup = new SqliteConnection($"Data Source={databasePath}"))
            {
                setup.Open();
                using var command = setup.CreateCommand();
                command.CommandText = """
                                      create table orders (id integer primary key, total real);
                                      create view big_orders as select id, total from orders where total > 100;
                                      """;
                command.ExecuteNonQuery();
            }

            var connection = new SqLiteConnectionSettings
            {
                Id = Guid.NewGuid(), Name = "Local", DatabaseType = DatabaseType.SqLite, Database = databasePath
            };

            var objectsSql = "select * from ";
            var objects = await SqlCompletionProvider.GetCompletionsAsync(connection, objectsSql, objectsSql.Length, SqlCompletionProvider.GetManualCompletionRequest(objectsSql, objectsSql.Length));
            Assert.Equal(CompletionItemKind.View, objects.OfType<SqlCompletionData>().Single(item => item.Text == "big_orders").Kind);

            var columnsSql = "select b. from big_orders b";
            var columns = await SqlCompletionProvider.GetCompletionsAsync(connection, columnsSql, "select b.".Length, SqlCompletionProvider.GetManualCompletionRequest(columnsSql, "select b.".Length));
            Assert.Contains(columns, item => item.Text == "total");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    private static void SeedObjects(Guid connectionId, params (string Name, NodeType NodeType, string[] Columns)[] objects)
    {
        SeedSchemaCache(connectionId, objects.Select(item => (item.Name, item.Columns)).ToArray());

        var cache = GetSeededCache(connectionId);
        var cacheType = cache.GetType();
        var tableNodes = (IDictionary)cacheType.GetProperty("TableNodes")!.GetValue(cache)!;
        var objectKinds = (IDictionary)cacheType.GetProperty("ObjectKinds")!.GetValue(cache)!;
        foreach (var (name, nodeType, _) in objects)
        {
            var node = CreateSchemaNode(nodeType, name);
            foreach (var key in new[] { name, $"[{name}]", $"`{name}`", $"\"{name}\"" })
                tableNodes[key] = node;
            objectKinds[name] = nodeType switch
            {
                NodeType.View => CompletionItemKind.View,
                NodeType.Synonym => CompletionItemKind.Synonym,
                _ => CompletionItemKind.Table
            };
        }
    }

    private static void SeedSequences(Guid connectionId, params string[] sequences)
    {
        if (!TryGetSeededCache(connectionId, out _))
            SeedSchemaCache(connectionId, Array.Empty<(string, string[])>());

        var cache = GetSeededCache(connectionId);
        var sequenceSet = (ISet<string>)cache.GetType().GetProperty("Sequences")!.GetValue(cache)!;
        foreach (var sequence in sequences)
            sequenceSet.Add(sequence);
    }

    private static object GetSeededCache(Guid connectionId)
    {
        return TryGetSeededCache(connectionId, out var cache) ? cache! : throw new InvalidOperationException("Schema cache not seeded.");
    }

    private static bool TryGetSeededCache(Guid connectionId, out object? cache)
    {
        var cacheDictionary = (IDictionary)typeof(SqlCompletionProvider).GetField("SchemaCache", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        cache = cacheDictionary.Contains(connectionId) ? cacheDictionary[connectionId] : null;
        return cache is not null;
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
