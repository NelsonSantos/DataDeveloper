using DataDeveloper.Enums;

namespace DataDeveloper.Models;

public sealed class AppDialogButton
{
    public required string Label { get; init; }
    public required DialogResult Result { get; init; }
    public bool IsDefault { get; init; }
    public bool IsCancel { get; init; }
    public bool IsPrimary { get; init; }
}
