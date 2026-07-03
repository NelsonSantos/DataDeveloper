using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DataDeveloper.ViewModels;

namespace DataDeveloper.Views;

public partial class ConnectionSelectorDialogView : UserControl
{
    public ConnectionSelectorDialogView()
    {
        InitializeComponent();
    }

    private void ConnectionCheckBox_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ConnectionSelectorViewModel viewModel)
            viewModel.RefreshBulkSelectionState();
    }

    private void ConnectionRow_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not ConnectionSelectorViewModel viewModel || viewModel.SelectedConnection is null)
            return;

        // Pass this view, not the tapped tree row: OkCommand saves first, which
        // rebuilds the tree's item containers and would orphan the row element.
        viewModel.OkCommand.Execute(this).Subscribe();
    }
}
