using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services.Metadata;
using DataDeveloper.Data.Services.SqlDialects;
using Xunit;

namespace DataDeveloper.Tests.Providers;

public class ObjectCatalogTests
{
    public static IEnumerable<object[]> AllProviders() =>
        Enum.GetValues<DatabaseType>().Select(databaseType => new object[] { databaseType });

    [Theory]
    [MemberData(nameof(AllProviders))]
    public void GetDdlRetrieval_SupportsTablesAndViewsOnEveryProvider(DatabaseType databaseType)
    {
        var catalog = ObjectCatalog.For(databaseType);

        Assert.NotNull(catalog.GetDdlRetrieval(new DbObjectRef(DbObjectKind.Table, null, "orders")));
        Assert.NotNull(catalog.GetDdlRetrieval(new DbObjectRef(DbObjectKind.View, null, "open_orders")));
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, true)]
    [InlineData(DatabaseType.Oracle, true)]
    [InlineData(DatabaseType.PostgresSql, true)]
    [InlineData(DatabaseType.MySql, true)]
    [InlineData(DatabaseType.SqLite, false)]
    public void GetDdlRetrieval_SupportsRoutinesWhereTheProviderHasThem(DatabaseType databaseType, bool supported)
    {
        var catalog = ObjectCatalog.For(databaseType);

        Assert.Equal(supported, catalog.GetDdlRetrieval(new DbObjectRef(DbObjectKind.Procedure, null, "process_orders")) is not null);
        Assert.Equal(supported, catalog.GetDdlRetrieval(new DbObjectRef(DbObjectKind.Function, null, "calculate_tax")) is not null);
    }

    [Theory]
    [MemberData(nameof(AllProviders))]
    public void GetDdlRetrieval_OnlyOracleTablesNeedSessionSetupOrPostProcessing(DatabaseType databaseType)
    {
        var retrieval = ObjectCatalog.For(databaseType).GetDdlRetrieval(new DbObjectRef(DbObjectKind.Table, null, "orders"))!;

        var isOracle = databaseType == DatabaseType.Oracle;
        Assert.Equal(isOracle, retrieval.SessionSetup is not null);
        Assert.Equal(isOracle, retrieval.PostProcess is not null);
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "@SchemaName")]
    [InlineData(DatabaseType.Oracle, ":SchemaName")]
    [InlineData(DatabaseType.PostgresSql, "@SchemaName")]
    [InlineData(DatabaseType.MySql, "@SchemaName")]
    public void TableStructureStatements_FilterByTheSchemaParameter(DatabaseType databaseType, string schemaParameter)
    {
        foreach (var statement in GetTableStructureStatements(databaseType))
        {
            Assert.Contains(schemaParameter, statement, StringComparison.Ordinal);
            Assert.Contains(schemaParameter.Replace("SchemaName", "TableName", StringComparison.Ordinal), statement, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SqLite_TableStructureStatements_UsePragmaFunctionsWithTableParameter()
    {
        foreach (var statement in GetTableStructureStatements(DatabaseType.SqLite))
        {
            Assert.Contains("pragma_", statement, StringComparison.Ordinal);
            Assert.Contains("(@TableName)", statement, StringComparison.Ordinal);
            Assert.DoesNotContain("__table_name__", statement, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "coalesce(@SchemaName")]
    [InlineData(DatabaseType.Oracle, "coalesce(upper(:SchemaName), user)")]
    [InlineData(DatabaseType.PostgresSql, "coalesce(cast(@SchemaName as text), current_schema())")]
    [InlineData(DatabaseType.MySql, "coalesce(@SchemaName, database())")]
    public void TableStructureStatements_FallBackToTheDefaultSchema(DatabaseType databaseType, string fallback)
    {
        if (databaseType == DatabaseType.SqlServer)
        {
            // SQL Server resolves an unqualified name through OBJECT_ID's own default-schema lookup.
            foreach (var statement in GetTableStructureStatements(databaseType))
                Assert.Contains("case when @SchemaName is null then quotename(@TableName)", statement, StringComparison.Ordinal);
            return;
        }

        foreach (var statement in GetTableStructureStatements(databaseType))
            Assert.Contains(fallback, statement, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServer_TableDdl_BuildsDefinitionFromSystemCatalog()
    {
        var retrieval = GetDdl(DatabaseType.SqlServer, DbObjectKind.Table, "dbo.Orders");

        Assert.Contains("declare @ObjectId int = object_id(N'dbo.Orders');", retrieval.Query, StringComparison.Ordinal);
        Assert.Contains("from sys.tables t", retrieval.Query, StringComparison.Ordinal);
        Assert.Contains("join sys.columns c", retrieval.Query, StringComparison.Ordinal);
        Assert.Contains("left join sys.identity_columns ic", retrieval.Query, StringComparison.Ordinal);
        Assert.Contains("left join sys.default_constraints dc", retrieval.Query, StringComparison.Ordinal);
        Assert.Contains("left join sys.computed_columns cc", retrieval.Query, StringComparison.Ordinal);
        Assert.Contains("from sys.key_constraints kc", retrieval.Query, StringComparison.Ordinal);
        Assert.Contains("from sys.check_constraints cc", retrieval.Query, StringComparison.Ordinal);
        Assert.Contains("from sys.foreign_keys fk", retrieval.Query, StringComparison.Ordinal);
        Assert.Contains("from sys.indexes i", retrieval.Query, StringComparison.Ordinal);
        Assert.Contains("as Definition", retrieval.Query, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DbObjectKind.View, "reporting.ActiveCustomers")]
    [InlineData(DbObjectKind.Procedure, "dbo.ProcessOrders")]
    [InlineData(DbObjectKind.Function, "dbo.CalculateTax")]
    public void SqlServer_ViewAndRoutineDdl_UseObjectDefinition(DbObjectKind kind, string name)
    {
        var retrieval = GetDdl(DatabaseType.SqlServer, kind, name);

        Assert.Equal($"select object_definition(object_id(N'{name}')) as Definition;", retrieval.Query);
    }

    [Fact]
    public void SqlServer_EscapesQuotesInObjectNames()
    {
        var retrieval = GetDdl(DatabaseType.SqlServer, DbObjectKind.View, "dbo.O'Brien");

        Assert.Equal("select object_definition(object_id(N'dbo.O''Brien')) as Definition;", retrieval.Query);
    }

    [Theory]
    [InlineData(DbObjectKind.Table, "sales.OrderItems", "show create table `sales`.`OrderItems`;")]
    [InlineData(DbObjectKind.View, "SalesSummary", "show create view `SalesSummary`;")]
    [InlineData(DbObjectKind.Procedure, "ProcessOrders", "show create procedure `ProcessOrders`;")]
    [InlineData(DbObjectKind.Function, "CalculateTax", "show create function `CalculateTax`;")]
    public void MySql_Ddl_UsesShowCreate(DbObjectKind kind, string name, string expected)
    {
        Assert.Equal(expected, GetDdl(DatabaseType.MySql, kind, name).Query);
    }

    [Fact]
    public void Postgres_TableDdl_UsesCatalogFunctions()
    {
        var query = GetDdl(DatabaseType.PostgresSql, DbObjectKind.Table, "sales.order_items").Query;

        Assert.Contains("from pg_class c", query, StringComparison.Ordinal);
        Assert.Contains("join pg_namespace n on n.oid = c.relnamespace", query, StringComparison.Ordinal);
        Assert.Contains("format_type(a.atttypid, a.atttypmod)", query, StringComparison.Ordinal);
        Assert.Contains("pg_get_expr(ad.adbin, ad.adrelid)", query, StringComparison.Ordinal);
        Assert.Contains("pg_get_constraintdef(con.oid, true)", query, StringComparison.Ordinal);
        Assert.Contains("pg_get_indexdef(i.indexrelid)", query, StringComparison.Ordinal);
        Assert.Contains("where n.nspname = 'sales'", query, StringComparison.Ordinal);
        Assert.Contains("and c.relname = 'order_items'", query, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DbObjectKind.Table)]
    [InlineData(DbObjectKind.View)]
    [InlineData(DbObjectKind.Function)]
    public void Postgres_UnqualifiedNames_DefaultToPublicSchema(DbObjectKind kind)
    {
        var query = GetDdl(DatabaseType.PostgresSql, kind, "orders").Query;

        Assert.Contains("n.nspname = 'public'", query, StringComparison.Ordinal);
    }

    [Fact]
    public void Postgres_ViewDdl_UsesPgGetViewdef()
    {
        var query = GetDdl(DatabaseType.PostgresSql, DbObjectKind.View, "reporting.open_orders").Query;

        Assert.Contains("pg_get_viewdef(c.oid, true)", query, StringComparison.Ordinal);
        Assert.Contains("where n.nspname = 'reporting'", query, StringComparison.Ordinal);
        Assert.Contains("and c.relname = 'open_orders'", query, StringComparison.Ordinal);
        Assert.Contains("c.relkind = 'v'", query, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DbObjectKind.Procedure)]
    [InlineData(DbObjectKind.Function)]
    public void Postgres_RoutineDdl_UsesPgGetFunctiondef(DbObjectKind kind)
    {
        var query = GetDdl(DatabaseType.PostgresSql, kind, "sales.recalculate").Query;

        Assert.Contains("select pg_get_functiondef(p.oid)", query, StringComparison.Ordinal);
        Assert.Contains("where n.nspname = 'sales'", query, StringComparison.Ordinal);
        Assert.Contains("and p.proname = 'recalculate'", query, StringComparison.Ordinal);
    }

    [Fact]
    public void Oracle_QualifiedTableDdl_UsesDbmsMetadataWithOwner()
    {
        var retrieval = GetDdl(DatabaseType.Oracle, DbObjectKind.Table, "HR.Orders");

        Assert.Equal("select dbms_metadata.get_ddl('TABLE', 'Orders', 'HR') as Definition from dual;", retrieval.Query);
        Assert.Contains("dbms_metadata.set_transform_param(dbms_metadata.session_transform, 'SQLTERMINATOR', true);", retrieval.SessionSetup, StringComparison.Ordinal);
        Assert.Contains("dbms_metadata.set_transform_param(dbms_metadata.session_transform, 'STORAGE', false);", retrieval.SessionSetup, StringComparison.Ordinal);
    }

    [Fact]
    public void Oracle_UnqualifiedTableDdl_UsesCurrentSchema()
    {
        var retrieval = GetDdl(DatabaseType.Oracle, DbObjectKind.Table, "ORDERS");

        Assert.Equal("select dbms_metadata.get_ddl('TABLE', 'ORDERS', sys_context('USERENV', 'CURRENT_SCHEMA')) as Definition from dual;", retrieval.Query);
    }

    [Fact]
    public void Oracle_ViewDdl_UsesUserViews()
    {
        var query = GetDdl(DatabaseType.Oracle, DbObjectKind.View, "HR.OPEN_ORDERS").Query;

        Assert.Contains("from user_views", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chr(10)", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("text_vc", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("view_name = 'OPEN_ORDERS'", query, StringComparison.Ordinal);
        Assert.Contains("create or replace view", query, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(DbObjectKind.Procedure)]
    [InlineData(DbObjectKind.Function)]
    public void Oracle_RoutineDdl_UsesUserSource(DbObjectKind kind)
    {
        var query = GetDdl(DatabaseType.Oracle, kind, "HR.CalculateTax").Query;

        Assert.Contains("from user_source", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'create or replace ' || ltrim(listagg(text, '') within group (order by line))", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name = 'CalculateTax'", query, StringComparison.Ordinal);
        Assert.Contains("type in ('PROCEDURE', 'FUNCTION')", query, StringComparison.Ordinal);
    }

    [Fact]
    public void Oracle_TableDdlPostProcessing_RemovesMetadataNoise()
    {
        var retrieval = GetDdl(DatabaseType.Oracle, DbObjectKind.Table, "DATADEVELOPER.ORDERS");
        var rawDdl = """
                       CREATE TABLE "DATADEVELOPER"."ORDERS" 
                          ( "ORDER_ID" NUMBER GENERATED BY DEFAULT ON NULL AS IDENTITY MINVALUE 1 MAXVALUE 9999999999999999999999999999 INCREMENT BY 1 START WITH 1 CACHE 20 NOORDER NOCYCLE NOKEEP NOSCALE NOT NULL ENABLE, 
                            "CUSTOMER_ID" NUMBER NOT NULL ENABLE, 
                            "ORDER_TOTAL" NUMBER(10,2) NOT NULL ENABLE, 
                            "STATUS" VARCHAR2(30) DEFAULT 'OPEN' NOT NULL ENABLE, 
                            "CREATED_AT" TIMESTAMP (6) DEFAULT current_timestamp NOT NULL ENABLE, 
                             PRIMARY KEY ("ORDER_ID")
                           USING INDEX ENABLE, 
                             CONSTRAINT "FK_ORDERS_CUSTOMERS" FOREIGN KEY ("CUSTOMER_ID")
                              REFERENCES "DATADEVELOPER"."CUSTOMERS" ("CUSTOMER_ID") ENABLE
                          ) ;
                       """;

        var ddl = retrieval.PostProcess!(rawDdl);

        Assert.DoesNotContain("MINVALUE", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ENABLE", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("USING INDEX", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"DATADEVELOPER\"", ddl, StringComparison.Ordinal);
        Assert.DoesNotContain("datadeveloper.", ddl, StringComparison.Ordinal);
        Assert.Contains("create table orders", ddl, StringComparison.Ordinal);
        Assert.Contains("\n    customer_id number", ddl, StringComparison.Ordinal);
        Assert.Contains("order_total number(10,2) not null", ddl, StringComparison.Ordinal);
        Assert.Contains("status varchar2(30) default 'open' not null", ddl, StringComparison.Ordinal);
        Assert.Contains("primary key", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("timestamp default current_timestamp", ddl, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(DbObjectKind.Table, "table")]
    [InlineData(DbObjectKind.View, "view")]
    public void SqLite_Ddl_UsesSqliteMaster(DbObjectKind kind, string type)
    {
        var query = GetDdl(DatabaseType.SqLite, kind, "orders").Query;

        Assert.Contains("from sqlite_master", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"type = '{type}'", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name = 'orders'", query, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetTableStructureStatements(DatabaseType databaseType)
    {
        var catalog = ObjectCatalog.For(databaseType);
        return
        [
            catalog.GetColumnDefaultsStatement(),
            catalog.GetPrimaryKeyStatement(),
            catalog.GetForeignKeysStatement(),
            catalog.GetIndexesStatement()
        ];
    }

    private static DdlRetrieval GetDdl(DatabaseType databaseType, DbObjectKind kind, string qualifiedName)
    {
        var databaseObject = DbObjectRef.Parse(kind, qualifiedName, SqlDialect.For(databaseType));
        var retrieval = ObjectCatalog.For(databaseType).GetDdlRetrieval(databaseObject);
        Assert.NotNull(retrieval);
        return retrieval!;
    }
}
