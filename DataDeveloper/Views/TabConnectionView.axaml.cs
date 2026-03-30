using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Services;
using DataDeveloper.ViewModels;

namespace DataDeveloper.Views;

public partial class TabConnectionView : UserControl
{
    private static readonly IBrush RailSelectedBackground = Brush.Parse("#3C3F41");
    private static readonly IBrush RailDefaultBackground = Brushes.Transparent;
    private static readonly IBrush RailSelectedForeground = Brush.Parse("#E6E6E6");
    private static readonly IBrush RailDefaultForeground = Brush.Parse("#9A9A9A");

    private const double MinimizedExplorerWidth = 0;
    private GridLength _previousExplorerWidth = new(1, GridUnitType.Star);

    public TabConnectionView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        ApplySchemaExplorerState();
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ApplySchemaExplorerState();
    }

    private void ToggleSchemaExplorer_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not TabConnectionViewModel viewModel)
            return;

        viewModel.IsSchemaExplorerMinimized = !viewModel.IsSchemaExplorerMinimized;
        ApplySchemaExplorerState();
    }

    private void ApplySchemaExplorerState()
    {
        if (DataContext is not TabConnectionViewModel viewModel)
            return;

        var explorerColumn = RootGrid.ColumnDefinitions[1];

        if (viewModel.IsSchemaExplorerMinimized)
        {
            if (explorerColumn.ActualWidth > 1)
                _previousExplorerWidth = new GridLength(explorerColumn.ActualWidth);

            explorerColumn.Width = new GridLength(MinimizedExplorerWidth);
            SchemaTreeView.IsVisible = false;
            SchemaExplorerSplitter.IsVisible = false;
            RefreshButton.IsVisible = false;
            NewQueryButton.IsVisible = false;
            ToggleSchemaExplorerButton.Background = RailDefaultBackground;
            SchemaRailIcon.Foreground = RailDefaultForeground;
            return;
        }

        explorerColumn.Width = _previousExplorerWidth.Value > 0
            ? _previousExplorerWidth
            : new GridLength(1, GridUnitType.Star);
        SchemaTreeView.IsVisible = true;
        SchemaExplorerSplitter.IsVisible = true;
        RefreshButton.IsVisible = true;
        NewQueryButton.IsVisible = true;
        ToggleSchemaExplorerButton.Background = RailSelectedBackground;
        SchemaRailIcon.Foreground = RailSelectedForeground;
    }

    private void SchemaNode_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            return;

        if (sender is not Control control || control.DataContext is not SchemaNode node)
            return;

        var menu = BuildContextMenu(node);
        if (menu is null)
            return;

        control.ContextMenu = menu;
        menu.Open(control);
        e.Handled = true;
    }

    private ContextMenu? BuildContextMenu(SchemaNode node)
    {
        if (DataContext is not TabConnectionViewModel viewModel)
            return null;

        var items = new List<object>();

        if (node.NodeType is NodeType.Table or NodeType.View or NodeType.Procedure or NodeType.Function or NodeType.Column or NodeType.Parameter)
        {
            items.Add(CreateMenuItem("Copy name", async () => await CopyToClipboardAsync(node.Name)));
            items.Add(CreateMenuItem("Copy qualified name", async () =>
                await CopyToClipboardAsync(DatabaseObjectScriptBuilder.BuildQualifiedName(viewModel.ConnectionSettings, node))));
        }

        if (node.NodeType is NodeType.Table or NodeType.View)
        {
            if (items.Count > 0)
                items.Add(new Separator());

            items.Add(CreateMenuItem("Open select script", () =>
            {
                viewModel.OpenQueryEditorWithScript(DatabaseObjectScriptBuilder.BuildSelectRowsScript(viewModel.ConnectionSettings, node));
                return Task.CompletedTask;
            }));

            items.Add(CreateMenuItem("Open count script", () =>
            {
                viewModel.OpenQueryEditorWithScript(DatabaseObjectScriptBuilder.BuildCountRowsScript(viewModel.ConnectionSettings, node));
                return Task.CompletedTask;
            }));
        }

        if (node.NodeType == NodeType.Procedure)
        {
            if (items.Count > 0)
                items.Add(new Separator());

            items.Add(CreateMenuItem("Open execute script", async () =>
            {
                await EnsureRoutineParametersLoadedAsync(node, viewModel);
                viewModel.OpenQueryEditorWithScript(DatabaseObjectScriptBuilder.BuildExecuteProcedureScript(viewModel.ConnectionSettings, node));
            }));
        }

        if (node.NodeType == NodeType.Function)
        {
            if (items.Count > 0)
                items.Add(new Separator());

            items.Add(CreateMenuItem("Open select function script", async () =>
            {
                await EnsureRoutineParametersLoadedAsync(node, viewModel);
                viewModel.OpenQueryEditorWithScript(DatabaseObjectScriptBuilder.BuildSelectFunctionScript(viewModel.ConnectionSettings, node));
            }));
        }

        return items.Count == 0 ? null : new ContextMenu { ItemsSource = items };
    }

    private MenuItem CreateMenuItem(string header, Func<Task> action)
    {
        var menuItem = new MenuItem { Header = header };
        menuItem.Click += async (_, _) => await action();
        return menuItem;
    }

    private async Task CopyToClipboardAsync(string text)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
            return;

        await topLevel.Clipboard.SetTextAsync(text);
    }

    private static async Task EnsureRoutineParametersLoadedAsync(SchemaNode node, TabConnectionViewModel viewModel)
    {
        var parameterFolder = node.Children.FirstOrDefault(child => child.NodeType == NodeType.Parameters);
        if (parameterFolder is null || !parameterFolder.CanLoad)
            return;

        await viewModel.SchemaExplorer.LoadNodeAsync(parameterFolder);
    }
}
