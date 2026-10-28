using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DataDeveloper.Data.Models;
using DataDeveloper.ViewModels;

namespace DataDeveloper.Views;

public partial class AiChatView : UserControl
{
    private AiChatViewModel? _viewModel;

    public AiChatView()
    {
        InitializeComponent();

        // The TextBox handles Enter itself (new line) before a bubbling KeyDown reaches us, so listen while tunnelling.
        DraftBox.AddHandler(KeyDownEvent, OnDraftKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
            _viewModel.Messages.CollectionChanged -= OnMessagesChanged;

        _viewModel = DataContext as AiChatViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Messages.CollectionChanged += OnMessagesChanged;
            Dispatcher.UIThread.Post(MessagesScroller.ScrollToEnd, DispatcherPriority.Background);
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            Dispatcher.UIThread.Post(MessagesScroller.ScrollToEnd, DispatcherPriority.Background);
    }

    // Enter sends (Shift+Enter adds a line), or - when the user chose it - Cmd/Ctrl+Enter sends and Enter adds a line.
    private void OnDraftKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _viewModel is null)
            return;

        var sendModifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        var shouldSend = _viewModel.SendWithModifier
            ? e.KeyModifiers.HasFlag(sendModifier)
            : !e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (!shouldSend)
            return;

        e.Handled = true;
        ICommand send = _viewModel.SendCommand;
        if (send.CanExecute(null))
            send.Execute(null);
    }

    private void OnSendModeMenuOpened(object? sender, EventArgs e)
    {
        EnterModeItem.IsChecked = _viewModel?.SendWithModifier != true;
        ModifierModeItem.IsChecked = _viewModel?.SendWithModifier == true;
    }

    private void OnEnterModeClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.SendWithModifier = false;
    }

    private void OnModifierModeClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.SendWithModifier = true;
    }

    // Built on open so the check mark follows the current model, including one typed by the user.
    private void OnModelMenuOpening(object? sender, EventArgs e)
    {
        if (_viewModel is null || sender is not MenuFlyout menu)
            return;

        var items = new List<object>();
        var current = _viewModel.Model?.Trim() ?? string.Empty;

        items.Add(CreateModelItem("Default model", string.Empty, current.Length == 0));
        items.Add(new Separator());
        foreach (var option in AiChatViewModel.ModelOptions)
            items.Add(CreateModelItem(option.Label, option.Id, string.Equals(option.Id, current, StringComparison.OrdinalIgnoreCase)));

        items.Add(new Separator());
        var custom = new MenuItem { Header = "Other model..." };
        custom.Click += (_, _) =>
        {
            if (Resources["CustomModelFlyout"] is Flyout flyout)
                flyout.ShowAt(ModelButton);
        };
        items.Add(custom);

        menu.Items.Clear();
        foreach (var item in items)
            menu.Items.Add(item);
    }

    private MenuItem CreateModelItem(string header, string id, bool isCurrent)
    {
        var item = new MenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.Radio,
            GroupName = "Model",
            IsChecked = isCurrent
        };
        item.Click += (_, _) =>
        {
            if (_viewModel is not null)
                _viewModel.Model = id;
        };
        return item;
    }

    private void OnCustomModelOpened(object? sender, EventArgs e)
    {
        if (sender is Flyout { Content: Control content })
            content.DataContext = _viewModel;
    }

    private async void OnCopyCodeClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is AiMessageSegmentViewModel segment && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(segment.Text);
    }

    private async void OnAttachFileClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this) is not { } topLevel)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Attach a text file",
            AllowMultiple = true,
            FileTypeFilter = [FilePickerFileTypes.All]
        });

        foreach (var file in files)
        {
            if (file.TryGetLocalPath() is { } path)
                await _viewModel.AttachFileAsync(path);
        }
    }

    private void OnAttachWholeQueryClick(object? sender, RoutedEventArgs e) => Execute(_viewModel?.AttachWholeQueryCommand);

    private void OnAttachSelectionClick(object? sender, RoutedEventArgs e) => Execute(_viewModel?.AttachSelectionCommand);

    private static void Execute(ICommand? command)
    {
        if (command?.CanExecute(null) == true)
            command.Execute(null);
    }

    private void OnPickObjectsClick(object? sender, RoutedEventArgs e)
    {
        if (this.Resources["ObjectPickerFlyout"] is Flyout flyout)
            flyout.ShowAt(AddButton);
    }

    private async void OnObjectPickerOpened(object? sender, EventArgs e)
    {
        if (_viewModel is null)
            return;

        // Flyout content lives outside this control's tree, so it doesn't inherit the DataContext.
        if (sender is Flyout { Content: Control content })
            content.DataContext = _viewModel;

        await _viewModel.LoadObjectChoicesAsync();
    }

    // Clicking a table adds it and keeps the popup open so several can be picked in a row.
    private void OnObjectSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox { SelectedItem: DbObjectRef selected } list || _viewModel is null)
            return;

        _viewModel.AttachObjects([selected]);
        list.SelectedItem = null;
    }
}
