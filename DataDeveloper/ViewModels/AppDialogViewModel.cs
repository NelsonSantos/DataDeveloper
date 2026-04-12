using System.Collections.Generic;
using Avalonia.Media;
using DataDeveloper.Core;
using DataDeveloper.Enums;
using DataDeveloper.Models;

namespace DataDeveloper.ViewModels;

public sealed class AppDialogViewModel : ViewModelBase
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string IconGlyph { get; init; }
    public required IBrush IconBackground { get; init; }
    public required IReadOnlyList<AppDialogButton> Buttons { get; init; }

    public static AppDialogViewModel Create(string message, string? title, DialogButtons buttons, DialogIcon icon)
    {
        return new AppDialogViewModel
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Data Developer" : title,
            Message = message,
            IconGlyph = GetIconGlyph(icon),
            IconBackground = new SolidColorBrush(Color.Parse(GetIconColor(icon))),
            Buttons = CreateButtons(buttons)
        };
    }

    public static AppDialogViewModel Create(string message, string? title, DialogIcon icon, IReadOnlyList<AppDialogButton> buttons)
    {
        return new AppDialogViewModel
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Data Developer" : title,
            Message = message,
            IconGlyph = GetIconGlyph(icon),
            IconBackground = new SolidColorBrush(Color.Parse(GetIconColor(icon))),
            Buttons = buttons
        };
    }

    private static IReadOnlyList<AppDialogButton> CreateButtons(DialogButtons buttons)
    {
        return buttons switch
        {
            DialogButtons.YesNo => [
                new AppDialogButton { Label = "No", Result = DialogResult.No },
                new AppDialogButton { Label = "Yes", Result = DialogResult.Yes, IsDefault = true, IsPrimary = true }
            ],
            DialogButtons.YesNoCancel => [
                new AppDialogButton { Label = "Cancel", Result = DialogResult.Cancel, IsCancel = true },
                new AppDialogButton { Label = "No", Result = DialogResult.No },
                new AppDialogButton { Label = "Yes", Result = DialogResult.Yes, IsDefault = true, IsPrimary = true }
            ],
            _ => [
                new AppDialogButton { Label = "OK", Result = DialogResult.Ok, IsDefault = true, IsPrimary = true, IsCancel = true }
            ]
        };
    }

    private static string GetIconGlyph(DialogIcon icon) => icon switch
    {
        DialogIcon.Warning => "\U000F0028",
        DialogIcon.Error => "\U000F0159",
        DialogIcon.Question => "\U000F02D7",
        DialogIcon.Success => "\U000F05E0",
        _ => "\U000F02FC"
    };

    private static string GetIconColor(DialogIcon icon) => icon switch
    {
        DialogIcon.Warning => "#C28A19",
        DialogIcon.Error => "#B44646",
        DialogIcon.Question => "#2962A3",
        DialogIcon.Success => "#2E7D5B",
        _ => "#4A6FA5"
    };
}
