using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Behaviors;

public static class TreeViewExpansionBehavior
{
    public static readonly AttachedProperty<bool> MonitorExpansionProperty =
        AvaloniaProperty.RegisterAttached<TreeViewItem, bool>(
            "MonitorExpansion", typeof(TreeViewExpansionBehavior));

    public static void SetMonitorExpansion(TreeViewItem element, bool value) =>
        element.SetValue(MonitorExpansionProperty, value);

    public static bool GetMonitorExpansion(TreeViewItem element) =>
        element.GetValue(MonitorExpansionProperty);

    static TreeViewExpansionBehavior()
    {
        MonitorExpansionProperty.Changed.AddClassHandler<TreeViewItem>((item, args) =>
        {
            if (args.NewValue is true)
            {
                item.Expanded += OnItemExpanded;
                item.Collapsed += OnItemCollapsed;
            }
            else
            {
                item.Expanded -= OnItemExpanded;
                item.Collapsed -= OnItemCollapsed;
            }
        });
    }

    private static async void OnItemExpanded(object? sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(sender, e.Source))
            return;

        if (sender is TreeViewItem treeViewItem &&
            treeViewItem.DataContext is SchemaNode node &&
            GetSchemaExplorer(treeViewItem) is ISchemaExplorer schemaExplorer)
        {
            node.IsExpanded = true;
            if (node.CanLoad && node.Next?.NodeType == NodeType.None)
            {
                await schemaExplorer.LoadNodeAsync(node);
            }
        }
    }

    private static void OnItemCollapsed(object? sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(sender, e.Source))
            return;

        if (sender is TreeViewItem treeViewItem &&
            treeViewItem.DataContext is SchemaNode node)
        {
            node.IsExpanded = false;
        }
    }

    // Permite injetar a dependência do ISchemaExplorer
    public static readonly AttachedProperty<ISchemaExplorer?> SchemaExplorerProperty =
        AvaloniaProperty.RegisterAttached<TreeViewItem, ISchemaExplorer?>(
            "SchemaExplorer", typeof(TreeViewExpansionBehavior));

    public static void SetSchemaExplorer(TreeViewItem element, ISchemaExplorer? value) =>
        element.SetValue(SchemaExplorerProperty, value);

    public static ISchemaExplorer? GetSchemaExplorer(TreeViewItem element) =>
        element.GetValue(SchemaExplorerProperty);
}
