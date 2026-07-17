using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models.SchemaCompare;

namespace DataDeveloper.Data.Services.SchemaCompare;

public static class SchemaCompareObjectEnumerator
{
    private static readonly Dictionary<NodeType, SchemaCompareObjectType> FolderTypeMap = new()
    {
        [NodeType.Tables] = SchemaCompareObjectType.Table,
        [NodeType.Views] = SchemaCompareObjectType.View,
        [NodeType.Procedures] = SchemaCompareObjectType.Procedure,
        [NodeType.Functions] = SchemaCompareObjectType.Function
    };

    public static async Task<IReadOnlyList<SchemaCompareObjectRef>> EnumerateAsync(IConnectionSettings connectionSettings)
    {
        var explorer = connectionSettings.GetSchemaExplorer();
        await explorer.InitializeSchemaNode();

        var connectionNode = explorer.RootConnections.FirstOrDefault();
        if (connectionNode is null)
            return Array.Empty<SchemaCompareObjectRef>();

        var results = new List<SchemaCompareObjectRef>();
        foreach (var folder in connectionNode.Children)
        {
            if (!FolderTypeMap.TryGetValue(folder.NodeType, out var objectType))
                continue;

            foreach (var node in folder.Children)
                results.Add(new SchemaCompareObjectRef { ObjectType = objectType, Name = node.Name });
        }

        return results;
    }
}
