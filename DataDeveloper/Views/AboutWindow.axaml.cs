using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace DataDeveloper.Views;

public partial class AboutWindow : Window
{
    private readonly Func<Task> _checkForUpdatesAsync;

    public AboutWindow()
    {
        InitializeComponent();
        _checkForUpdatesAsync = () => Task.CompletedTask;
    }

    public AboutWindow(string version, Func<Task> checkForUpdatesAsync)
        : this()
    {
        VersionText.Text = $"Version {version}";
        _checkForUpdatesAsync = checkForUpdatesAsync;
    }

    private void OnOkClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private async void OnCheckForUpdatesClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CheckForUpdatesButton.IsEnabled = false;
        try
        {
            await _checkForUpdatesAsync();
        }
        finally
        {
            CheckForUpdatesButton.IsEnabled = true;
        }
    }
}
