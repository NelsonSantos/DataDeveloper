using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models.TableDesigner;

namespace DataDeveloper.Data.Models.SchemaCompare;

public sealed class SchemaCompareObjectResult
{
    public SchemaCompareObjectType ObjectType { get; init; }
    public string Name { get; init; } = string.Empty;
    public SchemaCompareResultStatus Status { get; set; }
    public string? Script { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsIncludedByDefault { get; init; }

    // Table/New rows only, needed by NewTableDependencyOrderer.
    public TableDefinition? NewTableDefinition { get; init; }
}
