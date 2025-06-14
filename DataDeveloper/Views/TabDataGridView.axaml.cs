using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DataDeveloper.Models;
using DataDeveloper.ViewModels;

namespace DataDeveloper.Views;

public partial class TabDataGridView : UserControl
{
    private TabDataGridViewModel _model = null;
    public TabDataGridView()
    {
        InitializeComponent();
        DataGrid1.CopyingRowClipboardContent += DataGrid1OnCopyingRowClipboardContent;
    }

    private void DataGrid1OnCopyingRowClipboardContent(object? sender, DataGridRowClipboardEventArgs e)
    {
        e.ClipboardRowContent.RemoveAll(cell => cell.Column.DisplayIndex == 0);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        _model = this.DataContext as TabDataGridViewModel;
        if (_model != null)
        {
            _model.Headers.CollectionChanged -= HeadersOnCollectionChanged;
            _model.Headers.CollectionChanged += HeadersOnCollectionChanged;
        }
        base.OnDataContextChanged(e);
    }

    private void HeadersOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var column = default(DataGridTextColumn);
        
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            DataGrid1.Columns.Clear();
            column = new DataGridTextColumn
            {
                Header = "",
                Binding = new Avalonia.Data.Binding(nameof(RowValues.RowNumber)),
                IsReadOnly = true,
                Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
            };
            DataGrid1.Columns.Add(column);
        }
        else if (e.Action == NotifyCollectionChangedAction.Add)
        {
            var index = -1;
            foreach (var nome in e.NewItems)
            {
                index++;
                column = new DataGridTextColumn
                {
                    Header = nome,
                    Binding = new Avalonia.Data.Binding(nameof(RowValues.Value)),
                };
                DataGrid1.Columns.Add(column);
            }            
        }
    }
}