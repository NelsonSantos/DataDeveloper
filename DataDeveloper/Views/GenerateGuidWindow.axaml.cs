using Avalonia.Controls;
using DataDeveloper.ViewModels;

namespace DataDeveloper.Views;

public partial class GenerateGuidWindow : Window
{
    public GenerateGuidWindow()
    {
        InitializeComponent();

        Opened += (_, _) =>
        {
            if (DataContext is GenerateGuidViewModel viewModel)
                viewModel.AttachWindow(this);
        };
    }
}
