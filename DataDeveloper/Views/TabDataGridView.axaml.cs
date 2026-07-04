using System;
using Avalonia.Controls;
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
