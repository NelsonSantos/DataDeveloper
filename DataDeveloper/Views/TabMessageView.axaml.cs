using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DataDeveloper.Views;

public partial class TabMessageView : UserControl
{
    public TabMessageView()
    {
        InitializeComponent();
    }

    private void OnAskAiClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        (DataContext as ViewModels.TabMessageViewModel)?.AskAi();
}