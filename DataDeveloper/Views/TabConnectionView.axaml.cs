using Avalonia.Controls;
using Avalonia;
using Avalonia.Media;
using DataDeveloper.ViewModels;

namespace DataDeveloper.Views;

public partial class TabConnectionView : UserControl
{
    private static readonly IBrush RailSelectedBackground = Brush.Parse("#3C3F41");
    private static readonly IBrush RailDefaultBackground = Brushes.Transparent;
    private static readonly IBrush RailSelectedForeground = Brush.Parse("#E6E6E6");
    private static readonly IBrush RailDefaultForeground = Brush.Parse("#9A9A9A");

    private const double MinimizedExplorerWidth = 0;
    private GridLength _previousExplorerWidth = new(1, GridUnitType.Star);

    public TabConnectionView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        ApplySchemaExplorerState();
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ApplySchemaExplorerState();
    }

    private void ToggleSchemaExplorer_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not TabConnectionViewModel viewModel)
            return;

        viewModel.IsSchemaExplorerMinimized = !viewModel.IsSchemaExplorerMinimized;
        ApplySchemaExplorerState();
    }

    private void ApplySchemaExplorerState()
    {
        if (DataContext is not TabConnectionViewModel viewModel)
            return;

        var explorerColumn = RootGrid.ColumnDefinitions[1];

        if (viewModel.IsSchemaExplorerMinimized)
        {
            if (explorerColumn.ActualWidth > 1)
                _previousExplorerWidth = new GridLength(explorerColumn.ActualWidth);

            explorerColumn.Width = new GridLength(MinimizedExplorerWidth);
            SchemaTreeView.IsVisible = false;
            SchemaExplorerSplitter.IsVisible = false;
            RefreshButton.IsVisible = false;
            NewQueryButton.IsVisible = false;
            ToggleSchemaExplorerButton.Background = RailDefaultBackground;
            SchemaRailIcon.Foreground = RailDefaultForeground;
            return;
        }

        explorerColumn.Width = _previousExplorerWidth.Value > 0
            ? _previousExplorerWidth
            : new GridLength(1, GridUnitType.Star);
        SchemaTreeView.IsVisible = true;
        SchemaExplorerSplitter.IsVisible = true;
        RefreshButton.IsVisible = true;
        NewQueryButton.IsVisible = true;
        ToggleSchemaExplorerButton.Background = RailSelectedBackground;
        SchemaRailIcon.Foreground = RailSelectedForeground;
    }
}
