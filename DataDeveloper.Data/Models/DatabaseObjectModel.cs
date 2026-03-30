namespace DataDeveloper.Data.Models;

public class DatabaseObjectModel
{
    public string Name { get; set; } = string.Empty;
    public string? SpecificName { get; set; }
    public string? DataType { get; set; }
}
