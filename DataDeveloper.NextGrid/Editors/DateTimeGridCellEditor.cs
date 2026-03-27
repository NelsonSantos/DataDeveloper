using System.Globalization;

namespace DataDeveloper.NextGrid.Editors;

public sealed class DateTimeGridCellEditor : IGridCellEditor
{
    public bool CanEdit(Type? valueType, object? value)
    {
        var type = Nullable.GetUnderlyingType(valueType ?? value?.GetType() ?? typeof(object)) ?? valueType ?? value?.GetType();
        return type == typeof(DateTime) || type == typeof(DateTimeOffset);
    }

    public object? BeginEdit(object? value)
    {
        return value switch
        {
            DateTime dateTime => dateTime.ToString(CultureInfo.CurrentCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString(CultureInfo.CurrentCulture),
            _ => string.Empty
        };
    }

    public object? ApplyInput(object? currentValue, object? input)
    {
        return input?.ToString() ?? string.Empty;
    }

    public object? Commit(object? currentValue)
    {
        if (currentValue is null)
            return null;

        var text = currentValue.ToString() ?? string.Empty;
        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var dateTime))
            return dateTime;

        throw new FormatException($"Invalid date value '{text}'.");
    }
}
