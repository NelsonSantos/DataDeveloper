using System.Collections.Generic;

namespace DataDeveloper.NextGrid;

public interface IGridEditableRow : IReadOnlyList<object?>
{
    GridEditableRowVisualState VisualState { get; }
    bool HasValidationErrors { get; }
    IReadOnlyCollection<int> InvalidColumnIndexes { get; }
    string? GetValidationError(int columnIndex);
    void SetValue(int columnIndex, object? value);
}
