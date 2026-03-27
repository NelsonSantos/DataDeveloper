using DataDeveloper.NextGrid;
using Xunit;

namespace DataDeveloper.Tests.NextGrid;

public sealed class GridNavigationControllerTests
{
    [Fact]
    public void Navigate_Right_MovesToNextCell()
    {
        var controller = new GridNavigationController();

        var result = controller.Navigate(
            new GridNavigationRequest(new GridCellAddress(0, 1), GridNavigationDirection.Right, 100, 4));

        Assert.Equal(new GridCellAddress(0, 2), result);
    }

    [Fact]
    public void Navigate_Right_StopsAtLastColumn()
    {
        var controller = new GridNavigationController();

        var result = controller.Navigate(
            new GridNavigationRequest(new GridCellAddress(0, 4), GridNavigationDirection.Right, 100, 5));

        Assert.Equal(new GridCellAddress(0, 4), result);
    }

    [Fact]
    public void Navigate_Down_StopsAtLastRow()
    {
        var controller = new GridNavigationController();

        var result = controller.Navigate(
            new GridNavigationRequest(new GridCellAddress(99, 1), GridNavigationDirection.Down, 100, 3));

        Assert.Equal(new GridCellAddress(99, 1), result);
    }

    [Fact]
    public void Navigate_PageDown_UsesStep()
    {
        var controller = new GridNavigationController();

        var result = controller.Navigate(
            new GridNavigationRequest(new GridCellAddress(10, 1), GridNavigationDirection.PageDown, 100, 3, 9));

        Assert.Equal(new GridCellAddress(19, 1), result);
    }

    [Fact]
    public void Navigate_Home_GoesToFirstColumn()
    {
        var controller = new GridNavigationController();

        var result = controller.Navigate(
            new GridNavigationRequest(new GridCellAddress(10, 3), GridNavigationDirection.Home, 100, 5));

        Assert.Equal(new GridCellAddress(10, 0), result);
    }

    [Fact]
    public void Navigate_End_GoesToLastColumn()
    {
        var controller = new GridNavigationController();

        var result = controller.Navigate(
            new GridNavigationRequest(new GridCellAddress(10, 1), GridNavigationDirection.End, 100, 5));

        Assert.Equal(new GridCellAddress(10, 4), result);
    }
}
