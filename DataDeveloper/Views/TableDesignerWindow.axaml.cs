using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DataDeveloper.ViewModels;

namespace DataDeveloper.Views;

public partial class TableDesignerWindow : Window
{
    private const string ColumnDragFormat = "DataDeveloper.TableDesigner.Column";
    private TableDesignerColumnViewModel? _draggedColumn;

    public TableDesignerWindow()
    {
        InitializeComponent();
    }

    public TableDesignerWindow(TableDesignerViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        SqlPreview.Bind(TextEditorBindingHelper.BindableTextProperty, new Binding(nameof(TableDesignerViewModel.GeneratedSql)));

        if (DataContext is TableDesignerViewModel viewModel)
            Title = viewModel.IsEditMode ? "Edit table" : "Create table";
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private async void CopyGeneratedSql_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TableDesignerViewModel viewModel ||
            string.IsNullOrEmpty(viewModel.GeneratedSql))
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(viewModel.GeneratedSql);
    }

    private async void SaveGeneratedSql_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TableDesignerViewModel viewModel ||
            string.IsNullOrEmpty(viewModel.GeneratedSql))
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save generated SQL",
            SuggestedFileName = BuildGeneratedSqlFileName(viewModel),
            DefaultExtension = "sql",
            ShowOverwritePrompt = true,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("SQL file")
                {
                    Patterns = new[] { "*.sql" },
                    AppleUniformTypeIdentifiers = new[] { "public.sql" },
                    MimeTypes = new[] { "application/sql", "text/plain" }
                },
                FilePickerFileTypes.All
            }
        });

        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteAsync(viewModel.GeneratedSql);
    }

    private async void ColumnDragHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: TableDesignerColumnViewModel column })
            return;

        var pointer = e.GetCurrentPoint(this);
        if (!pointer.Properties.IsLeftButtonPressed)
            return;

        try
        {
            _draggedColumn = column;
            var data = new DataTransfer();
            data.Add(DataTransferItem.CreateText(ColumnDragFormat));
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        }
        finally
        {
            _draggedColumn = null;
        }
    }

    private void ColumnCard_DragOver(object? sender, DragEventArgs e)
    {
        if (sender is not Border border ||
            !e.DataTransfer.Contains(DataFormat.Text) ||
            _draggedColumn is null)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        SetColumnDropCue(border, IsDropAfter(e, border));
    }

    private void ColumnCard_DragLeave(object? sender, DragEventArgs e)
    {
        if (sender is Border border)
            ClearColumnDropCue(border);
    }

    private void ColumnCard_Drop(object? sender, DragEventArgs e)
    {
        if (DataContext is not TableDesignerViewModel viewModel ||
            sender is not Border { DataContext: TableDesignerColumnViewModel targetColumn } border ||
            _draggedColumn is not TableDesignerColumnViewModel sourceColumn ||
            ReferenceEquals(sourceColumn, targetColumn))
        {
            return;
        }

        var sourceIndex = viewModel.Columns.IndexOf(sourceColumn);
        var targetIndex = viewModel.Columns.IndexOf(targetColumn);

        if (sourceIndex < 0 || targetIndex < 0)
            return;

        var insertIndex = targetIndex + (IsDropAfter(e, border) ? 1 : 0);
        if (sourceIndex < insertIndex)
            insertIndex--;

        insertIndex = Math.Clamp(insertIndex, 0, viewModel.Columns.Count - 1);
        if (sourceIndex != insertIndex)
            viewModel.Columns.Move(sourceIndex, insertIndex);

        ClearColumnDropCue(border);
    }

    private static bool IsDropAfter(DragEventArgs e, Visual target)
    {
        var position = e.GetPosition(target);
        return position.Y > target.Bounds.Height / 2;
    }

    private static void SetColumnDropCue(Border border, bool after)
    {
        ClearColumnDropCue(border);
        border.Classes.Add(after ? "drop-after" : "drop-before");
    }

    private static void ClearColumnDropCue(Border border)
    {
        border.Classes.Remove("drop-before");
        border.Classes.Remove("drop-after");
    }

    private static string BuildGeneratedSqlFileName(TableDesignerViewModel viewModel)
    {
        var name = string.IsNullOrWhiteSpace(viewModel.TableName)
            ? "generated-table"
            : viewModel.TableName.Trim();

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
            name = name.Replace(invalidChar, '_');

        return name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
            ? name
            : $"{name}.sql";
    }
}
