using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models.SchemaCompare;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Models.SchemaCompare;

public class SchemaCompareObjectPickerItem : ReactiveObject
{
    public SchemaCompareObjectPickerItem(SchemaCompareObjectRef objectRef)
    {
        ObjectRef = objectRef;
        IsChecked = true;
    }

    public SchemaCompareObjectRef ObjectRef { get; }
    public SchemaCompareObjectType ObjectType => ObjectRef.ObjectType;
    public string Name => ObjectRef.Name;

    [Reactive] public bool IsChecked { get; set; }
}
