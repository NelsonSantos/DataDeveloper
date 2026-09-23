using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Interfaces;

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
            GetNodeLoader(treeViewItem) is ISchemaNodeLoader nodeLoader)
        {
            node.IsExpanded = true;
            if (node.CanLoad && node.Next?.NodeType == NodeType.None)
            {
                // The loader reports failures itself; an exception must not escape this async void handler.
                await nodeLoader.LoadSchemaNodeAsync(node);
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

    // Permite injetar quem carrega os filhos de um nó expandido
    public static readonly AttachedProperty<ISchemaNodeLoader?> NodeLoaderProperty =
        AvaloniaProperty.RegisterAttached<TreeViewItem, ISchemaNodeLoader?>(
            "NodeLoader", typeof(TreeViewExpansionBehavior));

    public static void SetNodeLoader(TreeViewItem element, ISchemaNodeLoader? value) =>
        element.SetValue(NodeLoaderProperty, value);

    public static ISchemaNodeLoader? GetNodeLoader(TreeViewItem element) =>
        element.GetValue(NodeLoaderProperty);
}
