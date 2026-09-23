namespace DataDeveloper.Data.Services;

public enum SchemaRefreshAction
{
    Unknown,
    Create,
    Alter,
    Drop,
}

public enum SchemaObjectType
{
    Unknown,
    Table,
    View,
    Procedure,
    Function,
    Trigger,
    Index,
}

public record SchemaRefreshTarget(SchemaRefreshAction Action, SchemaObjectType ObjectType, string? ObjectName);
