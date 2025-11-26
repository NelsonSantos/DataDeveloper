using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using DataDeveloper.Data.Enums;
using DataDeveloper.DataGrid;
using DataDeveloper.Models;
using DataDeveloper.ViewModels;
using ReactiveUI;

namespace DataDeveloper.Views;

public partial class TabDataGridView : UserControl
{
    private TabDataGridViewModel _model = null;
    public TabDataGridView()
    {
        InitializeComponent();
        DataGrid1.CopyingRowClipboardContent += DataGrid1OnCopyingRowClipboardContent;
        DataGrid1.AttachedToVisualTree += DataGrid1OnAttachedToVisualTree;
    }

    private void DataGrid1OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
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
            var index = -1;
            foreach (ColumnHeader columnHeader in e.NewItems)
            {
                index++;
                column = new DataGridTemplateColumn()
                {
                    Header = columnHeader.Name,
                    CellTemplate = new FuncDataTemplate<RowValues>((item, e) =>
                    {
                        var value = item?.Value;
                        var cell = new RenderCell();
                        cell.SetValue(RenderCell.HeaderProperty, columnHeader.Name);
                        cell.SetValue(RenderCell.TextProperty, value?.ToString() ?? "null");
                        cell.SetValue(RenderCell.ColumnAlignmentProperty, columnHeader.Alignment);
                        cell.SetValue(RenderCell.FontFamilyProperty, (FontFamily)Application.Current.Resources["MonospaceFont"]);
                        return cell;
                    },  true),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                };
                DataGrid1.Columns.Add(column);
            }            
        }
    }
}