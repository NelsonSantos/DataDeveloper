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
            DateTime dateTime => GridValueFormats.FormatDateTime(dateTime),
            DateTimeOffset dateTimeOffset => GridValueFormats.FormatDateTimeOffset(dateTimeOffset),
            _ => string.Empty
        };
    }

    public object? ApplyInput(object? currentValue, object? input)
    {
        return input switch
        {
            DateTime or DateTimeOffset => input,
            _ => input?.ToString() ?? string.Empty
        };
    }

    public object? Commit(object? currentValue)
    {
        if (currentValue is null)
            return null;

        if (currentValue is DateTime or DateTimeOffset)
            return currentValue;

        var text = currentValue.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (GridValueFormats.TryParseDateTime(text, out var dateTime))
            return dateTime;

        if (GridValueFormats.TryParseDateTimeOffset(text, out var dateTimeOffset))
            return dateTimeOffset;

        throw new FormatException($"Invalid date value '{text}'.");
    }
}
