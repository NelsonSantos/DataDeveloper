using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using DataDeveloper.Data;
using DataDeveloper.Models;
using DataDeveloper.Services;
using DataDeveloper.TemplateSelectors;
using DataDeveloper.ViewModels;

namespace DataDeveloper.Views;

public partial class TabQueryEditorView : UserControl
{
    private GridLength _previousTabHeight = new(200);
    private const double FallbackExpandedResultsHeight = 200;
    private TabQueryEditorViewModel? _viewModel;
    private readonly TabTemplateSelector? _templateSelector;
    private readonly CompletionInteractionState _completionInteractionState = new();
    private CompletionWindow? _completionWindow;
    private CompletionRequest? _pendingCompletionRequest;

    public TabQueryEditorView()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
        _templateSelector = this.Resources["TabContentTemplate"] as TabTemplateSelector;
        SqlEditor.TextArea.TextEntered += TextAreaOnTextEntered;
        SqlEditor.TextArea.TextEntering += TextAreaOnTextEntering;
        SqlEditor.TextArea.KeyDown += TextAreaOnKeyDown;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.ShowResultTool -= ViewModelOnShowResultTool;
            _viewModel.Tabs.CollectionChanged -= TabsOnCollectionChanged;
        }

        _viewModel = DataContext as TabQueryEditorViewModel;
        if (_viewModel is null)
            return;

        SqlEditor.Bind(TextEditorBindingHelper.BindableTextProperty, new Binding(nameof(TabQueryEditorViewModel.SqlStatement)) { Mode = BindingMode.TwoWay });
        SqlEditor.Bind(TextEditorBindingHelper.BindableSelectedTextProperty, new Binding(nameof(TabQueryEditorViewModel.SelectedStatement)));
        SqlEditor.Bind(TextEditorBindingHelper.BindableSelectionLengthProperty, new Binding(nameof(TabQueryEditorViewModel.SelectedStatementLength)));
        SqlEditor.Bind(TextEditorBindingHelper.BindableCaretOffsetProperty, new Binding(nameof(TabQueryEditorViewModel.CursorOffSet)));
        SqlEditor.Bind(TextEditorBindingHelper.BindableCaretLineProperty, new Binding(nameof(TabQueryEditorViewModel.CursorLine)));
        SqlEditor.Bind(TextEditorBindingHelper.BindableCaretColumnProperty, new Binding(nameof(TabQueryEditorViewModel.CursorColumn)));
    }
    
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        _viewModel.EditorHeadHeight = StackPanelEditor.Bounds.Height;
        _viewModel.ResultsHeaderHeight = StackPanelResult.Bounds.Height;
        _viewModel.ShowResultTool += ViewModelOnShowResultTool;
        _viewModel.Tabs.CollectionChanged += TabsOnCollectionChanged;
        ApplyResultsPanelState();
    }

    private void TabsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if ((e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Replace || e.Action == NotifyCollectionChangedAction.Reset) &&
            e.OldItems is not null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is BaseTabContent tab)
                    _templateSelector?.RemoveControl(tab);
            }
        }
    }

    private void ViewModelOnShowResultTool(object? sender, int e)
    {
        ApplyResultsPanelState();
    }

    private void ToggleTabs_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        _viewModel.ResultIsMinimized = !_viewModel.ResultIsMinimized;
        ApplyResultsPanelState();
    }

    private void ApplyResultsPanelState()
    {
        if (_viewModel is null)
            return;

        var tabRow = RootGrid.RowDefinitions[3];
        var collapsedHeight = StackPanelResult.Bounds.Height;
        var currentActualHeight = tabRow.ActualHeight;

        if (_viewModel.ResultIsMinimized)
        {
            if (currentActualHeight > collapsedHeight + 1)
                _previousTabHeight = tabRow.Height;

            tabRow.Height = new GridLength(collapsedHeight);
            Splitter.IsVisible = false;
            return;
        }

        if (currentActualHeight <= collapsedHeight + 1)
        {
            var expandedHeight = IsExpandedHeightCandidate(_previousTabHeight, collapsedHeight)
                ? _previousTabHeight
                : new GridLength(FallbackExpandedResultsHeight);

            tabRow.Height = expandedHeight;
        }

        Splitter.IsVisible = true;
    }

    private static bool IsExpandedHeightCandidate(GridLength length, double collapsedHeight)
    {
        return !length.IsAbsolute || length.Value > collapsedHeight + 1;
    }

    private async void TextAreaOnTextEntered(object? sender, TextInputEventArgs e)
    {
        var shouldReopen = _completionInteractionState.HandleTextEntered(e.Text);
        if (shouldReopen)
        {
            _pendingCompletionRequest = SqlCompletionProvider.GetAutoCompletionRequest(SqlEditor.Text ?? string.Empty, SqlEditor.CaretOffset, e.Text);
            Dispatcher.UIThread.Post(() =>
            {
                if (_pendingCompletionRequest is null)
                    return;

                var nextRequest = _pendingCompletionRequest;
                _pendingCompletionRequest = null;
                _ = ShowCompletionAsync(nextRequest, rememberAsAutoRequest: true);
            }, DispatcherPriority.Background);
            return;
        }

        if (!SqlCompletionProvider.ShouldTriggerCompletion(e.Text))
            return;

        var request = SqlCompletionProvider.GetAutoCompletionRequest(SqlEditor.Text ?? string.Empty, SqlEditor.CaretOffset, e.Text);
        if (request is null)
            return;

        await ShowCompletionAsync(request, rememberAsAutoRequest: true);
    }

    private void TextAreaOnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (!_completionInteractionState.ShouldRequestInsertion(e.Text, _completionWindow is not null))
            return;

        _completionWindow?.CompletionList.RequestInsertion(e);
    }

    private async void TextAreaOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var request = SqlCompletionProvider.GetManualCompletionRequest(SqlEditor.Text ?? string.Empty, SqlEditor.CaretOffset);
            await ShowCompletionAsync(request, rememberAsAutoRequest: false);
            e.Handled = true;
        }
    }

    private async Task ShowCompletionAsync(CompletionRequest request, bool rememberAsAutoRequest)
    {
        if (_viewModel is null)
            return;

        var completions = await SqlCompletionProvider.GetCompletionsAsync(
            _viewModel.ConnectionSettings,
            SqlEditor.Text ?? string.Empty,
            SqlEditor.CaretOffset,
            request);

        if (completions.Count == 0)
            return;

        if (rememberAsAutoRequest)
            _completionInteractionState.RememberAutoCompletion();

        _completionWindow?.Close();
        _completionWindow = new CompletionWindow(SqlEditor.TextArea);
        _completionWindow.StartOffset = SqlCompletionProvider.GetCompletionStartOffset(SqlEditor.Text ?? string.Empty, SqlEditor.CaretOffset);

        var data = _completionWindow.CompletionList.CompletionData;
        foreach (var completion in completions)
        {
            data.Add(completion);
        }

        _completionWindow.Closed += (_, _) =>
        {
            _completionWindow = null;
            _completionInteractionState.ResetWhitespaceReopen();
        };
        _completionWindow.Show();
    }
}
