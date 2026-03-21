using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Media;
using DataDeveloper.DataGrid;
using DataDeveloper.Models;
using DataDeveloper.ViewModels;

namespace DataDeveloper.Views;

public partial class TabDataGridView : UserControl
{
    private TabDataGridViewModel? _model;
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
        if (_model is not null)
            _model.Headers.CollectionChanged -= HeadersOnCollectionChanged;

        _model = this.DataContext as TabDataGridViewModel;
        if (_model != null)
        {
            _model.Headers.CollectionChanged += HeadersOnCollectionChanged;
        }
        base.OnDataContextChanged(e);
    }

    private void HeadersOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var column = default(DataGridTemplateColumn);
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            DataGrid1.Columns.Clear();
            column = new DataGridTemplateColumn()
            {
                Header = "",
                CellTemplate = new FuncDataTemplate<object>((item, _) =>
                {
                    return new TextBlock()
                    {
                        [!TextBlock.TextProperty] = new Avalonia.Data.Binding(nameof(RowValues.RowNumber)),
                    };
                    
                }, true),
                IsReadOnly = true,
                Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
            };
            DataGrid1.Columns.Add(column);
        }
        else if (e.Action == NotifyCollectionChangedAction.Add)
        {
            if (e.NewItems is null)
                return;

            foreach (ColumnHeader columnHeader in e.NewItems)
            {
                column = new DataGridTemplateColumn()
                {
                    Header = columnHeader.Name,
                    CellTemplate = new FuncDataTemplate<RowValues>((item, _) =>
                    {
                        var value = item?.Value;
                        var cell = new RenderCell();
                        cell.SetValue(RenderCell.HeaderProperty, columnHeader.Name);
                        cell.SetValue(RenderCell.TextProperty, value?.ToString() ?? "null");
                        cell.SetValue(RenderCell.ColumnAlignmentProperty, columnHeader.Alignment);
                        var fontFamily = Application.Current?.Resources["MonospaceFont"] as FontFamily;
                        if (fontFamily is not null)
                            cell.SetValue(RenderCell.FontFamilyProperty, fontFamily);
                        return cell;
                    },  true),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                };
                DataGrid1.Columns.Add(column);
            }            
        }
    }
}
