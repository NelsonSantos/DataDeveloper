namespace DataDeveloper.Data.Models;

/// <summary>
/// A table, view, procedure or function as listed by the provider's catalog.
/// </summary>
public class DatabaseObjectModel
{
    public string Name { get; set; } = string.Empty;
    public string? SchemaName { get; set; }

    /// <summary>
    /// Whether the object lives in the connection's default schema.
    /// </summary>
    public bool IsDefaultSchema { get; set; }

    /// <summary>
    /// Routine identity used to look up its parameters.
    /// </summary>
    public string? SpecificName { get; set; }

    /// <summary>
    /// Return type of a function.
    /// </summary>
    public string? DataType { get; set; }

    /// <summary>
    /// Extra information the catalog shows next to the object, e.g. a sequence's increment.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// The name shown in the schema tree: qualified with the schema only when the object is
    /// outside the connection's default schema.
    /// </summary>
    public string DisplayName => IsDefaultSchema || string.IsNullOrWhiteSpace(SchemaName)
        ? Name
        : $"{SchemaName}.{Name}";
}
