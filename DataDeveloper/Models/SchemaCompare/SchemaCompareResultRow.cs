using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models.SchemaCompare;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Models.SchemaCompare;

public class SchemaCompareResultRow : ReactiveObject
{
    public SchemaCompareResultRow(SchemaCompareObjectResult result)
    {
        Result = result;
        IsChecked = result.IsIncludedByDefault;
    }

    public SchemaCompareObjectResult Result { get; }
    public SchemaCompareObjectType ObjectType => Result.ObjectType;
    public string Name => Result.Name;
    public SchemaCompareResultStatus Status => Result.Status;
    public string? Script => Result.Script;
    public string? ErrorMessage => Result.ErrorMessage;

    public bool CanToggle =>
        Status is SchemaCompareResultStatus.New or SchemaCompareResultStatus.Changed or SchemaCompareResultStatus.OnlyInDestination;

    [Reactive] public bool IsChecked { get; set; }
}
