using System;
using System.Data.Common;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using DataDeveloper.Data;
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
        var sqlScriptItems = new List<object>();

        if (node.NodeType is NodeType.Table or NodeType.View or NodeType.Procedure or NodeType.Function or NodeType.Column or NodeType.Parameter)
        {
            items.Add(CreateMenuItem("Copy name", async () => await CopyToClipboardAsync(node.Name)));
            items.Add(CreateMenuItem("Copy qualified name", async () =>
                await CopyToClipboardAsync(DatabaseObjectScriptBuilder.BuildQualifiedName(viewModel.ConnectionSettings, node))));
        }

        if (node.NodeType is NodeType.Table or NodeType.View)
        {
            sqlScriptItems.Add(CreateMenuItem("Select rows", () =>
            {
                viewModel.OpenQueryEditorWithScript(DatabaseObjectScriptBuilder.BuildSelectRowsScript(viewModel.ConnectionSettings, node));
                return Task.CompletedTask;
            }));

            sqlScriptItems.Add(CreateMenuItem("Count rows", () =>
            {
                viewModel.OpenQueryEditorWithScript(DatabaseObjectScriptBuilder.BuildCountRowsScript(viewModel.ConnectionSettings, node));
                return Task.CompletedTask;
            }));
        }

        if (node.NodeType == NodeType.Table)
        {
            sqlScriptItems.Add(new Separator());
            sqlScriptItems.Add(CreateMenuItem("Insert", async () =>
            {
                await EnsureTableColumnsLoadedAsync(node, viewModel);
                viewModel.OpenQueryEditorWithScript(DatabaseObjectScriptBuilder.BuildInsertScript(viewModel.ConnectionSettings, node));
            }));

            sqlScriptItems.Add(CreateMenuItem("Update", async () =>
            {
                await EnsureTableColumnsLoadedAsync(node, viewModel);
                viewModel.OpenQueryEditorWithScript(DatabaseObjectScriptBuilder.BuildUpdateScript(viewModel.ConnectionSettings, node));
            }));

            sqlScriptItems.Add(CreateMenuItem("Delete", async () =>
            {
                await EnsureTableColumnsLoadedAsync(node, viewModel);
                viewModel.OpenQueryEditorWithScript(DatabaseObjectScriptBuilder.BuildDeleteScript(viewModel.ConnectionSettings, node));
            }));

            sqlScriptItems.Add(new Separator());
            sqlScriptItems.Add(CreateMenuItem("DDL Create to new Query", async () =>
            {
                await OpenTableDdlAsync(node, viewModel, openInEditor: true);
            }));

            sqlScriptItems.Add(CreateMenuItem("DDL Create to clipboard", async () =>
            {
                await OpenTableDdlAsync(node, viewModel, openInEditor: false);
            }));
        }

        if (node.NodeType == NodeType.View)
        {
            sqlScriptItems.Add(new Separator());
            sqlScriptItems.Add(CreateMenuItem("DDL Create to new Query", async () =>
            {
                await OpenDatabaseDdlAsync(node, viewModel, openInEditor: true);
            }));

            sqlScriptItems.Add(CreateMenuItem("DDL Create to clipboard", async () =>
            {
                await OpenDatabaseDdlAsync(node, viewModel, openInEditor: false);
            }));
        }

        if (node.NodeType == NodeType.Procedure)
        {
            sqlScriptItems.Add(CreateMenuItem("Execute procedure...", async () =>
            {
                await EnsureRoutineParametersLoadedAsync(node, viewModel);
                viewModel.OpenQueryEditorWithScript(DatabaseObjectScriptBuilder.BuildExecuteProcedureScript(viewModel.ConnectionSettings, node));
            }));

            sqlScriptItems.Add(new Separator());
            sqlScriptItems.Add(CreateMenuItem("DDL Create to new Query", async () =>
            {
                await OpenDatabaseDdlAsync(node, viewModel, openInEditor: true);
            }));

            sqlScriptItems.Add(CreateMenuItem("DDL Create to clipboard", async () =>
            {
                await OpenDatabaseDdlAsync(node, viewModel, openInEditor: false);
            }));
        }

        if (node.NodeType == NodeType.Function)
        {
            sqlScriptItems.Add(CreateMenuItem("Execute function...", async () =>
            {
                await EnsureRoutineParametersLoadedAsync(node, viewModel);
                viewModel.OpenQueryEditorWithScript(DatabaseObjectScriptBuilder.BuildSelectFunctionScript(viewModel.ConnectionSettings, node));
            }));

            sqlScriptItems.Add(new Separator());
            sqlScriptItems.Add(CreateMenuItem("DDL Create to new Query", async () =>
            {
                await OpenDatabaseDdlAsync(node, viewModel, openInEditor: true);
            }));

            sqlScriptItems.Add(CreateMenuItem("DDL Create to clipboard", async () =>
            {
                await OpenDatabaseDdlAsync(node, viewModel, openInEditor: false);
            }));
        }

        while (sqlScriptItems.Count > 0 && sqlScriptItems[^1] is Separator)
            sqlScriptItems.RemoveAt(sqlScriptItems.Count - 1);

        if (sqlScriptItems.Count > 0)
        {
            if (items.Count > 0)
                items.Add(new Separator());

            items.Add(new MenuItem { Header = "SQL Scripts", ItemsSource = sqlScriptItems });
        }

        if (node.NodeType is NodeType.Table or NodeType.View or NodeType.Procedure or NodeType.Function)
        {
            if (items.Count > 0)
                items.Add(new Separator());

            items.Add(CreateMenuItem(GetDropMenuText(node), async () =>
            {
                await DropObjectAsync(node, viewModel);
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

    private static async Task EnsureTableColumnsLoadedAsync(SchemaNode node, TabConnectionViewModel viewModel)
    {
        var columnFolder = node.Children.FirstOrDefault(child => child.NodeType == NodeType.Columns);
        if (columnFolder is null || !columnFolder.CanLoad)
            return;

        await viewModel.SchemaExplorer.LoadNodeAsync(columnFolder);
    }

    private async Task OpenDatabaseDdlAsync(SchemaNode node, TabConnectionViewModel viewModel, bool openInEditor)
    {
        var ddlQuery = DatabaseObjectScriptBuilder.TryBuildNativeDdlRetrievalScript(viewModel.ConnectionSettings, node)
                       ?? DatabaseObjectScriptBuilder.BuildObjectDdlRetrievalScript(viewModel.ConnectionSettings, node);
        await using var connection = viewModel.ConnectionSettings.GetDatabaseProvider().GetConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = ddlQuery;

        await using var reader = await command.ExecuteReaderAsync();
        var ddl = await ReadDdlAsync(reader);
        ddl = string.IsNullOrWhiteSpace(ddl) ? "-- DDL not found." : ddl;

        if (openInEditor)
            viewModel.OpenQueryEditorWithScript(ddl);
        else
            await CopyToClipboardAsync(ddl);
    }

    private async Task OpenTableDdlAsync(SchemaNode node, TabConnectionViewModel viewModel, bool openInEditor)
    {
        var nativeDdlQuery = DatabaseObjectScriptBuilder.TryBuildNativeDdlRetrievalScript(viewModel.ConnectionSettings, node);
        if (!string.IsNullOrWhiteSpace(nativeDdlQuery))
        {
            await OpenDatabaseDdlAsync(node, viewModel, openInEditor);
            return;
        }

        await EnsureTableColumnsLoadedAsync(node, viewModel);
        var ddl = DatabaseObjectScriptBuilder.BuildDdlScript(viewModel.ConnectionSettings, node);

        if (openInEditor)
            viewModel.OpenQueryEditorWithScript(ddl);
        else
            await CopyToClipboardAsync(ddl);
    }

    private static string GetDropMenuText(SchemaNode node)
    {
        var objectType = node.NodeType switch
        {
            NodeType.Table => "table",
            NodeType.View => "view",
            NodeType.Procedure => "procedure",
            NodeType.Function => "function",
            _ => "object"
        };

        return $"Drop {objectType} {node.Name}";
    }

    private static async Task DropObjectAsync(SchemaNode node, TabConnectionViewModel viewModel)
    {
        if (!await viewModel.ConfirmDropAsync(node))
            return;

        var dropScript = DatabaseObjectScriptBuilder.BuildDropScript(viewModel.ConnectionSettings, node);
        await viewModel.ExecuteBackgroundStatementAsync(dropScript, refreshSchema: true);
    }

    private static async Task<string> ReadDdlAsync(DbDataReader reader)
    {
        var parts = new List<string>();

        do
        {
            while (await reader.ReadAsync())
            {
                var createColumnIndex = Enumerable.Range(0, reader.FieldCount)
                    .FirstOrDefault(index => reader.GetName(index).StartsWith("Create ", StringComparison.OrdinalIgnoreCase), -1);

                if (createColumnIndex >= 0 && createColumnIndex < reader.FieldCount && !await reader.IsDBNullAsync(createColumnIndex))
                {
                    parts.Add(reader.GetString(createColumnIndex));
                    continue;
                }

                if (!await reader.IsDBNullAsync(0))
                    parts.Add(reader.GetString(0));
            }
        } while (await reader.NextResultAsync());

        return string.Join($"{Environment.NewLine}{Environment.NewLine}", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}
