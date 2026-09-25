using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using DataDeveloper.Docking;
using DataDeveloper.Models;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services.Metadata;
using DataDeveloper.Services;
using DataDeveloper.ViewModels;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DataDeveloper.TemplateSelectors;
using Dock.Avalonia.Controls;

namespace DataDeveloper.Views;

public partial class TabConnectionView : UserControl
{
    private TabConnectionViewModel? _viewModel;
    private readonly TabTemplateSelector? _templateSelector;
    private bool _isSyncingActiveEditor;

    public TabConnectionView()
    {
        InitializeComponent();
        _templateSelector = Resources["TabTemplateSelector"] as TabTemplateSelector;
        ConnectionDock.HostWindowFactory = () => new DocumentHostWindow();
        DockableLogicalOwner.AdoptLayout(ConnectionDock.Layout);
        if (ConnectionDock.Factory is { } factory)
        {
            factory.DockableClosing += OnDockableClosing;
            factory.ActiveDockableChanged += OnActiveDockableChanged;
        }

        // Clicking a query tab leaves keyboard focus on the tab strip, so shortcuts such as F5 would not reach
        // the editor; hand the focus to the editor instead (also when the clicked tab was already active).
        ConnectionDock.AddHandler(PointerReleasedEvent, OnDockPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>Items for the schema explorer tool dock (a single tool bound to this connection).</summary>
    public ObservableCollection<ConnectionToolItem> SchemaExplorerTools { get; } = new();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.QueryEditors.CollectionChanged -= OnQueryEditorsChanged;
        }

        _viewModel = DataContext as TabConnectionViewModel;
        SchemaExplorerTools.Clear();

        if (_viewModel is null)
            return;

        SchemaExplorerTools.Add(new ConnectionToolItem("Schema Explorer", _viewModel));
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.QueryEditors.CollectionChanged += OnQueryEditorsChanged;
    }

    // Closing a query document must go through the view model so unsaved changes prompt the user;
    // when it confirms, the editor leaves QueryEditors and Dock removes the document itself.
    private void OnDockableClosing(object? sender, DockableClosingEventArgs e)
    {
        if (_viewModel is null || e.Dockable is not IDocument { Context: TabQueryEditorViewModel editor })
            return;

        e.Cancel = true;
        _ = CloseEditorsAsync([editor]);
    }

    private async Task CloseEditorsAsync(IReadOnlyList<TabQueryEditorViewModel> editors)
    {
        if (_viewModel is null)
            return;

        foreach (var editor in editors)
        {
            if (!await _viewModel.CloseTabQueryEditor(editor))
                break;
        }
    }

    private void OnQueryEditorsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Reset)
            Dispatcher.UIThread.Post(RemoveEmptyFloatingWindows, DispatcherPriority.Background);
    }

    // Dock removes a closed editor's document, but a floating window that held it stays open empty.
    private void RemoveEmptyFloatingWindows()
    {
        if (ConnectionDock.Factory is not { } factory || ConnectionDock.Layout is not IRootDock { Windows: { } windows })
            return;

        foreach (var window in windows.ToList())
        {
            var hasContent = window.Layout is { } layout && ConnectionDockFactory.GetDockables(layout).Any(dockable => dockable is not IDock);
            if (!hasContent)
                factory.RemoveWindow(window);
        }
    }

    private void OnActiveDockableChanged(object? sender, ActiveDockableChangedEventArgs e)
    {
        if (_viewModel is null || e.Dockable is not IDocument { Context: TabQueryEditorViewModel editor })
            return;

        FocusEditor(editor);

        if (_isSyncingActiveEditor)
            return;

        var index = _viewModel.QueryEditors.IndexOf(editor);
        if (index < 0 || index == _viewModel.SelectedEditor)
            return;

        _isSyncingActiveEditor = true;
        try
        {
            _viewModel.SelectedEditor = index;
        }
        finally
        {
            _isSyncingActiveEditor = false;
        }
    }

    /// <summary>Activates the query tab <paramref name="step"/> positions away from the active one, wrapping around.</summary>
    public void ShowAdjacentQuery(int step)
    {
        if (ConnectionDock.Factory is not { } factory ||
            QueryDocuments.VisibleDockables?.OfType<IDocument>().ToList() is not { Count: > 1 } documents)
            return;

        var index = QueryDocuments.ActiveDockable is IDocument active ? documents.IndexOf(active) : -1;
        var next = documents[((index + step) % documents.Count + documents.Count) % documents.Count];
        factory.SetActiveDockable(next);
        factory.SetFocusedDockable(QueryDocuments, next);
    }

    private void OnDockPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left ||
            (e.Source as Visual)?.FindAncestorOfType<DocumentTabStripItem>(includeSelf: true) is not { DataContext: IDocument { Context: TabQueryEditorViewModel editor } })
            return;

        FocusEditor(editor);
    }

    private void FocusEditor(TabQueryEditorViewModel editor, bool retryUntilBuilt = true)
    {
        if (_templateSelector?.GetCachedControl(editor) is TabQueryEditorView view)
            view.FocusEditor();
        else if (retryUntilBuilt)
            // A new query's view is built after its document is activated.
            Dispatcher.UIThread.Post(() => FocusEditor(editor, retryUntilBuilt: false), DispatcherPriority.Background);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TabConnectionViewModel.SelectedEditor) || _isSyncingActiveEditor)
            return;

        // Defer until Dock has generated the document for a newly added editor.
        Dispatcher.UIThread.Post(ActivateSelectedEditorDocument, DispatcherPriority.Background);
    }

    private void ActivateSelectedEditorDocument()
    {
        if (_viewModel is null || ConnectionDock.Factory is not { } factory)
            return;

        var index = _viewModel.SelectedEditor;
        if (index < 0 || index >= _viewModel.QueryEditors.Count)
            return;

        if (factory.GetContainerFromItem(_viewModel.QueryEditors[index]) is not IDockable document)
            return;

        _isSyncingActiveEditor = true;
        try
        {
            factory.SetActiveDockable(document);
            if (document.Owner is IDock owner)
                factory.SetFocusedDockable(owner, document);
        }
        finally
        {
            _isSyncingActiveEditor = false;
        }
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

        if (node.NodeType is NodeType.Table or NodeType.View or NodeType.Procedure or NodeType.Function or NodeType.Sequence or NodeType.Synonym or NodeType.Column or NodeType.Parameter)
        {
            items.Add(CreateMenuItem("Copy name", async () => await CopyToClipboardAsync(node.Name)));
            items.Add(CreateMenuItem("Copy qualified name", async () =>
                await CopyToClipboardAsync(DatabaseObjectScriptBuilder.BuildQualifiedName(viewModel.ConnectionSettings, node))));
        }

        if (node.NodeType is NodeType.PrimaryKey or NodeType.UniqueKey or NodeType.ForeignKey or NodeType.CheckConstraint or NodeType.Index or NodeType.Trigger)
            items.Add(CreateMenuItem("Copy name", async () => await CopyToClipboardAsync(node.Name)));

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
                await OpenDdlAsync(node, viewModel, openInEditor: true);
            }));

            sqlScriptItems.Add(CreateMenuItem("DDL Create to clipboard", async () =>
            {
                await OpenDdlAsync(node, viewModel, openInEditor: false);
            }));
        }

        if (node.NodeType == NodeType.Sequence)
        {
            sqlScriptItems.Add(CreateMenuItem("Select next value", () =>
            {
                viewModel.OpenQueryEditorWithScript(DatabaseObjectScriptBuilder.BuildSelectNextValueScript(viewModel.ConnectionSettings, node));
                return Task.CompletedTask;
            }));
        }

        if (node.NodeType is NodeType.View or NodeType.Trigger or NodeType.Sequence or NodeType.Synonym)
        {
            sqlScriptItems.Add(new Separator());
            sqlScriptItems.Add(CreateMenuItem("DDL Create to new Query", async () =>
            {
                await OpenDdlAsync(node, viewModel, openInEditor: true);
            }));

            sqlScriptItems.Add(CreateMenuItem("DDL Create to clipboard", async () =>
            {
                await OpenDdlAsync(node, viewModel, openInEditor: false);
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
                await OpenDdlAsync(node, viewModel, openInEditor: true);
            }));

            sqlScriptItems.Add(CreateMenuItem("DDL Create to clipboard", async () =>
            {
                await OpenDdlAsync(node, viewModel, openInEditor: false);
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
                await OpenDdlAsync(node, viewModel, openInEditor: true);
            }));

            sqlScriptItems.Add(CreateMenuItem("DDL Create to clipboard", async () =>
            {
                await OpenDdlAsync(node, viewModel, openInEditor: false);
            }));
        }

        while (sqlScriptItems.Count > 0 && sqlScriptItems[0] is Separator)
            sqlScriptItems.RemoveAt(0);

        while (sqlScriptItems.Count > 0 && sqlScriptItems[^1] is Separator)
            sqlScriptItems.RemoveAt(sqlScriptItems.Count - 1);

        if (sqlScriptItems.Count > 0)
        {
            if (items.Count > 0)
                items.Add(new Separator());

            items.Add(new MenuItem { Header = "SQL Scripts", ItemsSource = sqlScriptItems });
        }

        if (node.NodeType == NodeType.Tables)
        {
            items.Add(CreateMenuItem("New table...", async () =>
            {
                await viewModel.CreateTableAsync(this);
            }));
        }

        if (node.NodeType == NodeType.Table)
        {
            if (items.Count > 0)
                items.Add(new Separator());

            items.Add(CreateMenuItem("Edit table...", async () =>
            {
                await viewModel.EditTableAsync(node, this);
            }));
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

    private async Task OpenDdlAsync(SchemaNode node, TabConnectionViewModel viewModel, bool openInEditor)
    {
        var ddl = await new SchemaMetadataService(viewModel.ConnectionSettings).GetDdlAsync(node);
        ddl = string.IsNullOrWhiteSpace(ddl)
            ? "-- DDL not available: the object was not found, or the connection user lacks permission to view its definition."
            : ddl;

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

}
