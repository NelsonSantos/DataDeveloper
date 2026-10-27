using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DataDeveloper.ViewModels;

namespace DataDeveloper.Views;

public partial class PreferencesDialogView : UserControl
{
    public PreferencesDialogView()
    {
        InitializeComponent();
    }

    private async void OnBrowseExecutableClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PreferencesViewModel viewModel || TopLevel.GetTopLevel(this) is not { } topLevel)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select the command line tool",
            AllowMultiple = false,
            FileTypeFilter = [FilePickerFileTypes.All]
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
            viewModel.ExecutablePath = path;
    }
}
