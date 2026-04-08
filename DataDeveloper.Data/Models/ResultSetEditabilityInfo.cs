namespace DataDeveloper.Data.Models;

public sealed record ResultSetEditabilityInfo(
    bool IsEditable,
    string? TableName,
    string? Reason);
