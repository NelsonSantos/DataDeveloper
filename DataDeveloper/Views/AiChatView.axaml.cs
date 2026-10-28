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

        BuildModelMenu();
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

    // Built once from AiChatViewModel.ModelOptions, so the list lives in a single place. Items must exist before the
    // menu opens: an empty MenuFlyout shows up as a thin empty line.
    private void BuildModelMenu()
    {
        if (ModelButton.Flyout is not MenuFlyout menu)
            return;

        menu.Items.Add(CreateModelItem("Default model", string.Empty));
        menu.Items.Add(new Separator());

        foreach (var option in AiChatViewModel.ModelOptions)
            menu.Items.Add(CreateModelItem(option.Label, option.Id));

        menu.Items.Add(new Separator());
        var other = new MenuItem { Header = "Other model..." };
        other.Click += OnOtherModelClick;
        menu.Items.Add(other);
    }

    private MenuItem CreateModelItem(string header, string id)
    {
        var item = new MenuItem { Header = header, Tag = id, ToggleType = MenuItemToggleType.Radio, GroupName = "Model" };
        item.Click += OnModelItemClick;
        return item;
    }

    // The check mark follows the current model, including one typed by the user (which matches no item).
    private void OnModelMenuOpening(object? sender, EventArgs e)
    {
        if (_viewModel is null || sender is not MenuFlyout menu)
            return;

        var current = _viewModel.Model?.Trim() ?? string.Empty;
        foreach (var item in menu.Items.OfType<MenuItem>().Where(i => i.Tag is string))
            item.IsChecked = string.Equals((string)item.Tag!, current, StringComparison.OrdinalIgnoreCase);
    }

    private void OnModelItemClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && sender is MenuItem { Tag: string id })
            _viewModel.Model = id;
    }

    private void OnOtherModelClick(object? sender, RoutedEventArgs e)
    {
        if (Resources["CustomModelFlyout"] is Flyout flyout)
            flyout.ShowAt(ModelButton);
    }

    private void OnCustomModelOpened(object? sender, EventArgs e)
    {
        if (sender is not Flyout { Content: Control content })
            return;

        content.DataContext = _viewModel;
        (content as StackPanel)?.Children.OfType<TextBox>().FirstOrDefault()?.Focus();
    }

    // Enter (or Tab, which just leaves the field) confirms the typed model and closes the popup.
    private void OnCustomModelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape))
            return;

        e.Handled = true;
        if (Resources["CustomModelFlyout"] is Flyout flyout)
            flyout.Hide();
    }

    private void OnCustomModelDone(object? sender, RoutedEventArgs e)
    {
        if (Resources["CustomModelFlyout"] is Flyout flyout)
            flyout.Hide();
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
