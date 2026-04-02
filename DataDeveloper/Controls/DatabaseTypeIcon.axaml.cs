using Avalonia;
using Avalonia.Controls;

namespace DataDeveloper.Controls;

public partial class DatabaseTypeIcon : UserControl
{
    public static readonly StyledProperty<object?> DatabaseTypeProperty =
        AvaloniaProperty.Register<DatabaseTypeIcon, object?>(nameof(DatabaseType));

    public static readonly StyledProperty<double> BadgeSizeProperty =
        AvaloniaProperty.Register<DatabaseTypeIcon, double>(nameof(BadgeSize), 28d);

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<DatabaseTypeIcon, double>(nameof(IconSize), 18d);

    public static readonly StyledProperty<Thickness> BadgePaddingProperty =
        AvaloniaProperty.Register<DatabaseTypeIcon, Thickness>(nameof(BadgePadding), new Thickness(4));

    public DatabaseTypeIcon()
    {
        InitializeComponent();
    }

    public object? DatabaseType
    {
        get => GetValue(DatabaseTypeProperty);
        set => SetValue(DatabaseTypeProperty, value);
    }

    public double BadgeSize
    {
        get => GetValue(BadgeSizeProperty);
        set => SetValue(BadgeSizeProperty, value);
    }

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public Thickness BadgePadding
    {
        get => GetValue(BadgePaddingProperty);
        set => SetValue(BadgePaddingProperty, value);
    }
}
