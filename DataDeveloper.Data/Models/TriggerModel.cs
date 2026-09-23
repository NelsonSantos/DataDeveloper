namespace DataDeveloper.Data.Models;

/// <summary>
/// A trigger defined on a table, as listed by the provider's catalog.
/// </summary>
public sealed class TriggerModel
{
    public string? SchemaName { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// When the trigger fires, in lower case, e.g. "after", "before each row", "instead of".
    /// </summary>
    public string Timing { get; set; } = string.Empty;

    /// <summary>
    /// The statements that fire it, in lower case, e.g. "insert, update".
    /// </summary>
    public string Events { get; set; } = string.Empty;

    /// <summary>
    /// The CREATE TRIGGER text, where the catalog needs it to fill <see cref="Timing"/> and <see cref="Events"/>.
    /// </summary>
    public string? Definition { get; set; }
}
