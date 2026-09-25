using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using DataDeveloper.ViewModels;

namespace DataDeveloper.Views;

/// <summary>Ctrl+Tab switcher panel; the keyboard is handled by MainWindow, clicking an entry switches to it.</summary>
public partial class QuerySwitcherView : UserControl
{
    public QuerySwitcherView()
    {
        InitializeComponent();
    }

    private void OnConnectionTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not QuerySwitcherViewModel switcher || TappedItemIndex(ConnectionList, e) is not { } index)
            return;

        switcher.HighlightConnection(index);
        switcher.Commit();
    }

    private void OnEditorTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not QuerySwitcherViewModel switcher || TappedItemIndex(EditorList, e) is not { } index)
            return;

        switcher.HighlightEditor(index);
        switcher.Commit();
    }

    private static int? TappedItemIndex(ListBox list, TappedEventArgs e) =>
        (e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { } item ? list.IndexFromContainer(item) : null;
}
