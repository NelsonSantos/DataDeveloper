using DataDeveloper.Models;
using Xunit;

namespace DataDeveloper.Tests;

public class EditableGridRowTests
{
    [Fact]
    public void SetValue_MarksExistingRowAsModified()
    {
        var row = new EditableGridRow(["1", "Alice"]);

        row.SetValue(1, "Bob");

        Assert.Equal(EditableGridRowState.Modified, row.State);
        Assert.True(row.HasPendingChanges);
        Assert.Equal("Bob", row[1]);
    }

    [Fact]
    public void RejectChanges_RestoresOriginalValues()
    {
        var row = new EditableGridRow(["1", "Alice"]);
        row.SetValue(1, "Bob");

        row.RejectChanges();

        Assert.Equal(EditableGridRowState.Clean, row.State);
        Assert.False(row.HasPendingChanges);
        Assert.Equal("Alice", row[1]);
    }

    [Fact]
    public void NewRow_RemainsNewAfterValueChanges()
    {
        var row = new EditableGridRow([null, null], isNew: true);

        row.SetValue(0, 1);

        Assert.Equal(EditableGridRowState.New, row.State);
        Assert.True(row.HasPendingChanges);
    }

    [Fact]
    public void MarkDeleted_SetsDeletedState()
    {
        var row = new EditableGridRow(["1", "Alice"]);

        row.MarkDeleted();

        Assert.Equal(EditableGridRowState.Deleted, row.State);
        Assert.True(row.HasPendingChanges);
    }

    [Fact]
    public void SetValidationError_MarksColumnAsInvalid()
    {
        var row = new EditableGridRow(["1", null], isNew: true);

        row.SetValidationError(1, "'nome' is required.");

        Assert.True(row.HasValidationErrors);
        Assert.Equal(1, row.ValidationErrorCount);
        Assert.Empty(row.InvalidColumnIndexes);
        Assert.Equal("'nome' is required.", row.GetValidationError(1));
    }

    [Fact]
    public void TouchedColumn_WithValidationError_BecomesVisiblyInvalid()
    {
        var row = new EditableGridRow(["1", null], isNew: true);
        row.SetValidationError(1, "'nome' is required.");

        row.SetValue(1, null);

        Assert.Contains(1, row.InvalidColumnIndexes);
    }

    [Fact]
    public void ClearValidationErrors_RemovesInvalidColumns()
    {
        var row = new EditableGridRow(["1", null], isNew: true);
        row.SetValidationError(1, "'nome' is required.");

        row.ClearValidationErrors();

        Assert.False(row.HasValidationErrors);
        Assert.Empty(row.InvalidColumnIndexes);
        Assert.Equal(0, row.ValidationErrorCount);
        Assert.Null(row.GetValidationError(1));
    }
}
