using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DataDeveloper.Services;
using DataDeveloper.ViewModels;
using DataDeveloper.NextGrid.UI;

namespace DataDeveloper.Views;

public partial class TabDataGridView : UserControl
{
    public TabDataGridView()
    {
        InitializeComponent();
        ResultGrid.CellEditCommitted += OnCellEditCommitted;
        ResultGrid.StructuredTextCellViewRequested += OnStructuredTextCellViewRequested;
        ResultGrid.GotFocus += OnResultGridGotFocus;
        Unloaded += OnUnloaded;
    }

    private void OnResultGridGotFocus(object? sender, RoutedEventArgs e)
    {
        GetMainWindowViewModel()?.SetActiveGrid(ResultGrid);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        GetMainWindowViewModel()?.ClearActiveGrid(ResultGrid);
    }

    private MainWindowViewModel? GetMainWindowViewModel()
    {
        return this.TryGetParentWindow()?.DataContext as MainWindowViewModel;
    }

    private void OnCellEditCommitted(object? sender, GridCellEditCommittedEventArgs e)
    {
        if (DataContext is TabDataGridViewModel viewModel)
            viewModel.NotifyCellEdited(e.Result.Cell.Row);
    }

    private async void OnStructuredTextCellViewRequested(object? sender, GridStructuredTextCellViewRequestedEventArgs e)
    {
        if (DataContext is not TabDataGridViewModel viewModel)
            return;

        if (TopLevel.GetTopLevel(this) is not Window ownerWindow)
            return;

        var result = await viewModel.ShowStructuredTextCellDialogAsync(ownerWindow, e.Value, e.IsEditable, e.Kind);
        if (result is not null)
            ResultGrid.CommitStructuredTextCellEdit(result);
        else
            ResultGrid.CancelStructuredTextCellEdit();
    }
}
