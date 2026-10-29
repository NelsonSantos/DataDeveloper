using System;
using Avalonia;
using Avalonia.Controls;

namespace DataDeveloper.Views;

/// <summary>The Return key, optionally preceded by the platform's send modifier (Command on macOS, Ctrl elsewhere).</summary>
public partial class KeyHintIcon : UserControl
{
    public static readonly StyledProperty<bool> ShowModifierProperty =
        AvaloniaProperty.Register<KeyHintIcon, bool>(nameof(ShowModifier));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<KeyHintIcon, double>(nameof(Size), 14);

    public KeyHintIcon()
    {
        InitializeComponent();
        Refresh();
    }

    public bool ShowModifier
    {
        get => GetValue(ShowModifierProperty);
        set => SetValue(ShowModifierProperty, value);
    }

    /// <summary>Height of the icons; the text, when shown, scales with it.</summary>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ShowModifierProperty || change.Property == SizeProperty)
            Refresh();
    }

    private void Refresh()
    {
        if (CommandIcon is null)
            return;

        var isMac = OperatingSystem.IsMacOS();
        CommandIcon.IsVisible = ShowModifier && isMac;
        CtrlText.IsVisible = ShowModifier && !isMac;
        CommandIcon.Width = CommandIcon.Height = Size;
        ReturnIcon.Width = ReturnIcon.Height = Size;
        CtrlText.FontSize = Size * 0.85;
    }
}
