using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class DestructiveSqlDetectorTests
{
    public static TheoryData<DatabaseType> Providers =>
    [
        DatabaseType.SqlServer,
        DatabaseType.Oracle,
        DatabaseType.PostgresSql,
        DatabaseType.MySql,
        DatabaseType.SqLite
    ];

    private static DestructiveSqlKind? Single(string sql, DatabaseType type)
    {
        var found = DestructiveSqlDetector.Find(sql, type);
        return found.Count == 0 ? null : found.Single().Kind;
    }

    [Theory, MemberData(nameof(Providers))]
    public void Drop_IsFlagged(DatabaseType type)
    {
        Assert.Equal(DestructiveSqlKind.Drop, Single("drop table orders", type));
        Assert.Equal(DestructiveSqlKind.Drop, Single("DROP VIEW v;", type));
    }

    [Theory, MemberData(nameof(Providers))]
    public void Truncate_IsFlagged(DatabaseType type) =>
        Assert.Equal(DestructiveSqlKind.Truncate, Single("truncate table orders", type));

    [Theory, MemberData(nameof(Providers))]
    public void DeleteAndUpdateWithoutWhere_AreFlagged(DatabaseType type)
    {
        Assert.Equal(DestructiveSqlKind.DeleteWithoutWhere, Single("delete from orders", type));
        Assert.Equal(DestructiveSqlKind.UpdateWithoutWhere, Single("update orders set total = 0", type));
    }

    [Theory, MemberData(nameof(Providers))]
    public void DeleteAndUpdateWithWhere_AreNotFlagged(DatabaseType type)
    {
        Assert.Null(Single("delete from orders where id = 5", type));
        Assert.Null(Single("update orders set total = 0 where id in (select id from old_orders)", type));
    }

    [Theory, MemberData(nameof(Providers))]
    public void WhereThatIsAlwaysTrue_IsFlagged(DatabaseType type)
    {
        Assert.Equal(DestructiveSqlKind.AlwaysTrueWhere, Single("delete from orders where 1 = 1", type));
        Assert.Equal(DestructiveSqlKind.AlwaysTrueWhere, Single("update orders set x = 1 where 1=1;", type));
        Assert.Equal(DestructiveSqlKind.AlwaysTrueWhere, Single("delete from orders where id = 5 or 1 = 1", type));
        Assert.Equal(DestructiveSqlKind.AlwaysTrueWhere, Single("delete from orders where 'a' = 'a'", type));
        Assert.Null(Single("delete from orders where 1 = 2", type));
        Assert.Null(Single("delete from orders where id = 1 and 1 = 1", type));
    }

    [Theory, MemberData(nameof(Providers))]
    public void AlterTableDrop_IsFlagged_ButDroppingADefaultIsNot(DatabaseType type)
    {
        Assert.Equal(DestructiveSqlKind.AlterDrop, Single("alter table orders drop column note", type));
        Assert.Equal(DestructiveSqlKind.AlterDrop, Single("alter table orders drop constraint fk_orders", type));
        Assert.Null(Single("alter table orders alter column note drop default", type));
        Assert.Null(Single("alter table orders add note varchar(10)", type));
    }

    [Theory, MemberData(nameof(Providers))]
    public void HarmlessStatements_AreNotFlagged(DatabaseType type)
    {
        Assert.Empty(DestructiveSqlDetector.Find("select * from orders where id = 1", type));
        Assert.Empty(DestructiveSqlDetector.Find("insert into orders (id) values (1)", type));
        Assert.Empty(DestructiveSqlDetector.Find("create table t (id int)", type));
        Assert.Empty(DestructiveSqlDetector.Find("", type));
    }

    [Theory, MemberData(nameof(Providers))]
    public void KeywordsInCommentsAndStrings_AreIgnored(DatabaseType type)
    {
        Assert.Empty(DestructiveSqlDetector.Find("select 'drop table x; delete from y' as t", type));
        Assert.Empty(DestructiveSqlDetector.Find("/* drop table x */ select 1", type));
        Assert.Empty(DestructiveSqlDetector.Find("-- truncate table x\nselect 1", type));
        Assert.Empty(DestructiveSqlDetector.Find("select \"drop\" from t", type));
    }

    [Theory, MemberData(nameof(Providers))]
    public void AWhereInsideAStringDoesNotCountAsAWhere(DatabaseType type) =>
        Assert.Equal(DestructiveSqlKind.UpdateWithoutWhere, Single("update orders set note = 'where is it'", type));

    [Theory, MemberData(nameof(Providers))]
    public void CommentedOutWhere_DoesNotCountAsAWhere(DatabaseType type)
    {
        Assert.Equal(DestructiveSqlKind.DeleteWithoutWhere, Single("delete from orders -- where id = 1", type));
        Assert.Equal(DestructiveSqlKind.DeleteWithoutWhere, Single("delete from orders /* where id = 1 */", type));
    }

    [Theory, MemberData(nameof(Providers))]
    public void CteBeforeDelete_IsRecognised(DatabaseType type)
    {
        if (type is DatabaseType.Oracle)
            return;

        Assert.Equal(
            DestructiveSqlKind.DeleteWithoutWhere,
            Single("with old as (select id from orders where id < 5) delete from orders_copy", type));
    }

    [Theory, MemberData(nameof(Providers))]
    public void EveryRiskyStatementOfAScriptIsReported(DatabaseType type)
    {
        var found = DestructiveSqlDetector.Find("select 1; drop table a; update b set x = 1; delete from c where id = 1; truncate table d;", type);

        Assert.Equal(
            [DestructiveSqlKind.Drop, DestructiveSqlKind.UpdateWithoutWhere, DestructiveSqlKind.Truncate],
            found.Select(f => f.Kind));
    }

    [Fact]
    public void SqlServer_QuotedIdentifierNamedWhere_IsNotAWhere() =>
        Assert.Equal(DestructiveSqlKind.UpdateWithoutWhere, Single("update t set [where] = 1", DatabaseType.SqlServer));

    [Fact]
    public void MySql_BackslashEscapedQuote_KeepsTheStringOpen() =>
        Assert.Equal(DestructiveSqlKind.UpdateWithoutWhere, Single(@"update t set a = 'it\'s where we are'", DatabaseType.MySql));

    [Fact]
    public void MySql_HashComment_HidesTheWhere() =>
        Assert.Equal(DestructiveSqlKind.DeleteWithoutWhere, Single("delete from t # where id = 1", DatabaseType.MySql));

    [Fact]
    public void PostgreSql_DollarQuotedBody_IsOneLiteral() =>
        Assert.Empty(DestructiveSqlDetector.Find("select $$ drop table x; delete from y $$", DatabaseType.PostgresSql));

    [Fact]
    public void PostgreSql_EscapeString_HonoursBackslashes() =>
        Assert.Equal(DestructiveSqlKind.UpdateWithoutWhere, Single(@"update t set a = E'it\'s where'", DatabaseType.PostgresSql));

    [Fact]
    public void Oracle_AlternativeQuoteLiteral_IsOneLiteral() =>
        Assert.Equal(DestructiveSqlKind.UpdateWithoutWhere, Single("update t set a = q'[it's where we are]'", DatabaseType.Oracle));

    [Fact]
    public void Description_ExplainsEveryKind()
    {
        foreach (var kind in Enum.GetValues<DestructiveSqlKind>())
            Assert.False(string.IsNullOrWhiteSpace(new DestructiveStatement(kind, "x").Description));
    }
}
