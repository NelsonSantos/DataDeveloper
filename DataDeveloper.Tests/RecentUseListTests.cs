using DataDeveloper.Models;
using Xunit;

namespace DataDeveloper.Tests;

public class RecentUseListTests
{
    [Fact]
    public void Order_PutsMostRecentlyTouchedFirst_ThenUntouchedInTheirOrder()
    {
        var list = new RecentUseList<string>();
        list.Touch("a");
        list.Touch("c");

        Assert.Equal(["c", "a", "b", "d"], list.Order(["a", "b", "c", "d"]));
    }

    [Fact]
    public void Touch_MovesAnAlreadyUsedItemBackToTheFront()
    {
        var list = new RecentUseList<string>();
        list.Touch("a");
        list.Touch("b");
        list.Touch("a");

        Assert.Equal(["a", "b"], list.Order(["a", "b"]));
    }

    [Fact]
    public void Order_IgnoresRemovedAndMissingItems()
    {
        var list = new RecentUseList<string>();
        list.Touch("a");
        list.Touch("b");
        list.Touch("c");
        list.Remove("b");

        Assert.Equal(["c", "d"], list.Order(["c", "d"]));
    }
}
