using Avalonia.Controls;

namespace DataDeveloper.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    public SplashWindow(string version)
        : this()
    {
        VersionText.Text = $"Version {version}";
    }
}
