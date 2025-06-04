using System.Collections.ObjectModel;
using DataDeveloper.Data.Enums;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Data.Models;

public class SchemaNode : ReactiveObject
{
    internal SchemaNode(NodeType nodeType, string name, bool isFolder, SchemaNode? parent, bool canLoad = false, string? details = null, object? tag = null)
    {
        NodeType = nodeType;
        Name = name;
        IsFolder = isFolder;
        Parent = parent;
        CanLoad = canLoad;
        Details = details;
        Tag = tag;
        
        Children = new ObservableCollection<SchemaNode>();
        
        if (isFolder && canLoad)
            Children.Add(new SchemaNode(NodeType.None, "", isFolder: false, this, canLoad: false));
    }

    public NodeType NodeType { get; }
    public string Name { get; }
    public bool IsFolder { get; }
    public SchemaNode? Parent { get; }
    public SchemaNode? Next => Children.FirstOrDefault();
    [Reactive] public bool CanLoad { get; set; }
    public string? Details { get; }
    public object? Tag { get; }
    public ObservableCollection<SchemaNode> Children { get; }
}