using DataDeveloper.Data.Enums;

namespace DataDeveloper.Data.Models.SchemaCompare;

public sealed class SchemaCompareObjectRef
{
    public SchemaCompareObjectType ObjectType { get; init; }
    public string Name { get; init; } = string.Empty;
}
