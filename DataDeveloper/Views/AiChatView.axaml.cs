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
    private Flyout? _objectsFlyout;

    public AiChatView()
    {
        InitializeComponent();
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

    // Enter sends; Shift+Enter keeps the TextBox's own new line.
    private void OnDraftKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift) || _viewModel is null)
            return;

        e.Handled = true;
        ICommand send = _viewModel.SendCommand;
        if (send.CanExecute(null))
            send.Execute(null);
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

    private async void OnObjectsFlyoutOpened(object? sender, System.EventArgs e)
    {
        if (_viewModel is null)
            return;

        // Flyout content lives outside this control's tree, so it doesn't inherit the DataContext.
        if (sender is Flyout { Content: Control content } flyout)
        {
            _objectsFlyout = flyout;
            content.DataContext = _viewModel;
        }

        await _viewModel.LoadObjectChoicesAsync();
    }

    private void OnAttachSelectedObjectsClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Control { Parent: Panel panel })
            return;

        var list = panel.Children.OfType<ListBox>().FirstOrDefault();
        if (list is not null)
            _viewModel.AttachObjects(list.SelectedItems?.OfType<DbObjectRef>().ToList() ?? []);

        _objectsFlyout?.Hide();
    }
}
