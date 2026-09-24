using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models.SchemaCompare;
using ReactiveUI;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

namespace DataDeveloper.Models.SchemaCompare;

public partial class SchemaCompareObjectPickerItem : ReactiveObject
{
    public SchemaCompareObjectPickerItem(SchemaCompareObjectRef objectRef)
    {
        ObjectRef = objectRef;
        IsChecked = true;
    }

    public SchemaCompareObjectRef ObjectRef { get; }
    public SchemaCompareObjectType ObjectType => ObjectRef.ObjectType;
    public string Name => ObjectRef.Name;

    [Reactive] private bool _isChecked;
}
