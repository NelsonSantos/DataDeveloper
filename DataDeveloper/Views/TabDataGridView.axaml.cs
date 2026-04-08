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
    }

    private void OnCellEditCommitted(object? sender, GridCellEditCommittedEventArgs e)
    {
        if (DataContext is TabDataGridViewModel viewModel)
            viewModel.NotifyCellEdited(e.Result.Cell.Row);
    }
}
