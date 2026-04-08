using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using DataDeveloper.NextGrid;

namespace DataDeveloper.Models;

public sealed class EditableGridRow : IGridEditableRow
{
    private readonly object?[] _originalValues;
    private readonly object?[] _currentValues;
    private readonly Dictionary<int, string> _validationErrors = [];
    private readonly HashSet<int> _touchedColumns = [];

    public EditableGridRow(object?[] values, bool isNew = false)
    {
        _originalValues = (object?[])values.Clone();
        _currentValues = (object?[])values.Clone();
        State = isNew ? EditableGridRowState.New : EditableGridRowState.Clean;
    }

    public EditableGridRowState State { get; private set; }

    public GridEditableRowVisualState VisualState => State switch
    {
        EditableGridRowState.New => GridEditableRowVisualState.New,
        EditableGridRowState.Modified => GridEditableRowVisualState.Modified,
        _ => GridEditableRowVisualState.Clean
    };

    public bool HasPendingChanges => State != EditableGridRowState.Clean;
    public bool HasValidationErrors => _validationErrors.Count > 0;
    public IReadOnlyCollection<int> InvalidColumnIndexes => _validationErrors.Keys.Where(_touchedColumns.Contains).ToList();
    public int ValidationErrorCount => _validationErrors.Count;

    public object? this[int index] => _currentValues[index];

    public int Count => _currentValues.Length;

    public IReadOnlyList<object?> OriginalValues => _originalValues;

    public string? GetValidationError(int columnIndex)
    {
        return _validationErrors.GetValueOrDefault(columnIndex);
    }

    public void SetValue(int columnIndex, object? value)
    {
        _currentValues[columnIndex] = value;
        _touchedColumns.Add(columnIndex);
        RecalculateState();
    }

    public void MarkDeleted()
    {
        if (State == EditableGridRowState.New)
            return;

        State = EditableGridRowState.Deleted;
    }

    public void UnmarkDeleted()
    {
        RecalculateState();
    }

    public void RejectChanges()
    {
        Array.Copy(_originalValues, _currentValues, _originalValues.Length);
        State = EditableGridRowState.Clean;
    }

    public void AcceptChanges()
    {
        Array.Copy(_currentValues, _originalValues, _currentValues.Length);
        State = EditableGridRowState.Clean;
    }

    public void ClearValidationErrors()
    {
        _validationErrors.Clear();
    }

    public void SetValidationError(int columnIndex, string message)
    {
        _validationErrors[columnIndex] = message;
    }

    public IEnumerator<object?> GetEnumerator()
    {
        return ((IEnumerable<object?>)_currentValues).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _currentValues.GetEnumerator();
    }

    private void RecalculateState()
    {
        if (State == EditableGridRowState.New)
            return;

        State = _currentValues.SequenceEqual(_originalValues)
            ? EditableGridRowState.Clean
            : EditableGridRowState.Modified;
    }
}
