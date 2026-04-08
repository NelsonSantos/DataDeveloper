namespace DataDeveloper.Data.Models;

public sealed record EditableResultSetMetadata(
    ResultSetEditabilityInfo Editability,
    IReadOnlyList<ColumnModel> TableColumns)
{
    public string? TableName => Editability.TableName;
    public bool IsEditable => Editability.IsEditable;
    public string? Reason => Editability.Reason;
    public IReadOnlyList<ColumnModel> PrimaryKeyColumns => TableColumns.Where(column => column.IsPrimaryKey).ToList();
}
