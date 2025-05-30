using System.Collections.ObjectModel;
using DataDeveloper.Data.Enums;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Data.Models;

public class SchemaNode : ReactiveObject
{
    internal SchemaNode(NodeType nodeType, string name, bool isFolder, SchemaNode? parent, bool canLoad = false)
    {
        NodeType = nodeType;
        Name = name;
        IsFolder = isFolder;
        Parent = parent;
        CanLoad = canLoad;
        Children = new ObservableCollection<SchemaNode>();
    }

    public NodeType NodeType { get; }
    public string Name { get; }
    public bool IsFolder { get; }
    public SchemaNode? Parent { get; }
    [Reactive] public bool CanLoad { get; set; }
    [Reactive] public ObservableCollection<SchemaNode> Children { get; }
}