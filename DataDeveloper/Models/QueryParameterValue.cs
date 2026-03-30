using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Models;

public class QueryParameterValue : ReactiveObject
{
    public QueryParameterValue(string name, string? value = null)
    {
        Name = name;
        _value = value;
        _isNull = string.IsNullOrWhiteSpace(value);
    }

    public string Name { get; }

    private string? _value;
    public string? Value
    {
        get => _value;
        set
        {
            this.RaiseAndSetIfChanged(ref _value, value);
            if (!string.IsNullOrWhiteSpace(value) && IsNull)
                IsNull = false;
        }
    }

    private bool _isNull;
    public bool IsNull
    {
        get => _isNull;
        set
        {
            this.RaiseAndSetIfChanged(ref _isNull, value);
            if (value)
                return;

            if (string.IsNullOrWhiteSpace(Value))
                Value = string.Empty;
        }
    }
}
