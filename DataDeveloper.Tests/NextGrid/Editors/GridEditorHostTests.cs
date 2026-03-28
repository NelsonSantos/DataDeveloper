using DataDeveloper.NextGrid;
using DataDeveloper.NextGrid.Editors;
using Xunit;

namespace DataDeveloper.Tests.NextGrid.Editors;

public sealed class GridEditorHostTests
{
    [Fact]
    public void BeginApplyCommit_RunsEditingCycle()
    {
        var host = new GridEditorHost(new GridEditorRegistry());

        var session = host.BeginEdit(new GridCellAddress(2, 3), typeof(string), "old");
        var updated = host.ApplyInput("new");
        var result = host.Commit();

        Assert.Equal("old", session.OriginalValue);
        Assert.Equal("new", updated.CurrentValue);
        Assert.True(result.Committed);
        Assert.Equal("new", result.NewValue);
        Assert.Null(host.CurrentSession);
    }

    [Fact]
    public void Cancel_RestoresOriginalValue()
    {
        var host = new GridEditorHost(new GridEditorRegistry());

        host.BeginEdit(new GridCellAddress(1, 1), typeof(string), "before");
        host.ApplyInput("after");
        var result = host.Cancel();

        Assert.False(result.Committed);
        Assert.Equal("before", result.NewValue);
        Assert.Null(host.CurrentSession);
    }

    [Fact]
    public void Commit_NumberEditorParsesText()
    {
        var host = new GridEditorHost(new GridEditorRegistry());

        host.BeginEdit(new GridCellAddress(0, 0), typeof(decimal), 10.5m);
        host.ApplyInput("227,5");
        var result = host.Commit();

        Assert.Equal(227.5m, result.NewValue);
    }
}
