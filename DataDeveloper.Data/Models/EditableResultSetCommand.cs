namespace DataDeveloper.Data.Models;

public sealed record EditableResultSetCommand(
    string Sql,
    IReadOnlyDictionary<string, object?> Parameters);
