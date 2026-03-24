using System.Collections.Generic;

namespace DataDeveloper.Models;

public class RowValues
{
    private readonly object?[] _values;
    private int _index = -1;
    public RowValues(int rowNumber, object?[] values)
    {
        RowNumber = rowNumber;
        _values = values;
    }
    
    public int RowNumber { get; }
    public int CurrentIndex => _index;
    public IReadOnlyList<object?> Values => _values;

    public object? GetValueAt(int index)
    {
        return index >= 0 && index < _values.Length
            ? _values[index]
            : null;
    }

    public object? Value{
        get
        {
            _index++;

            if (_index >= _values.Length)
                _index = 0;
            
            return _values[_index];
        }
    }
}
