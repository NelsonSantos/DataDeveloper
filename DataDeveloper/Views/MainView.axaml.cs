using System.IO;
using System.Reactive.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DataDeveloper.ViewModels;

namespace DataDeveloper.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void OpenRecentMenuItem_OnSubmenuOpened(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || DataContext is not MainWindowViewModel viewModel)
            return;

        menuItem.Items.Clear();

        foreach (var filePath in viewModel.RecentFiles)
        {
            var item = new MenuItem { Header = Path.GetFileName(filePath) };
            item.Click += async (_, _) => await viewModel.OpenRecentFileCommand.Execute(filePath).ToTask();
            menuItem.Items.Add(item);
        }

        if (menuItem.Items.Count == 0)
            return;

        menuItem.Items.Add(new Separator());

        var clearItem = new MenuItem { Header = "Clear items" };
        clearItem.Click += async (_, _) => await viewModel.ClearRecentFilesCommand.Execute().ToTask();
        menuItem.Items.Add(clearItem);
    }
}
