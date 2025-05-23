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
    public object Value{
        get
        {
            _index++;
            return _values[_index];
        }
    }
}