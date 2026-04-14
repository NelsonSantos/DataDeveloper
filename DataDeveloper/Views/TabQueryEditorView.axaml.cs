using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive;
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
using ReactiveUI;

namespace DataDeveloper.Views;

public partial class TabQueryEditorView : UserControl
{
    private GridLength _previousTabHeight = new(200);
    private GridLength _previousParametersPanelWidth = new(320);
    private const double FallbackExpandedResultsHeight = 200;
    private const double DefaultParametersPanelWidth = 320;
    private const double MinimumParametersPanelWidth = 220;
    private TabQueryEditorViewModel? _viewModel;
    private readonly TabTemplateSelector? _templateSelector;
    private readonly CompletionInteractionState _completionInteractionState = new();
    private CompletionWindow? _completionWindow;
    private OverloadInsightWindow? _functionInsightWindow;
    private SqlFunctionOverloadProvider? _functionOverloadProvider;
    private string? _activeFunctionInsightName;
    private CompletionRequest? _pendingCompletionRequest;

    public TabQueryEditorView()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
        _templateSelector = this.Resources["TabContentTemplate"] as TabTemplateSelector;
        SqlEditor.TextArea.TextEntered += TextAreaOnTextEntered;
        SqlEditor.TextArea.TextEntering += TextAreaOnTextEntering;
        SqlEditor.TextArea.KeyDown += TextAreaOnKeyDown;
        SqlEditor.GotFocus += SqlEditorOnGotFocus;
        SqlEditor.TextChanged += SqlEditorOnStateChanged;
        SqlEditor.TextArea.SelectionChanged += SqlEditorOnStateChanged;
        Unloaded += OnUnloaded;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.ShowResultTool -= ViewModelOnShowResultTool;
            _viewModel.Tabs.CollectionChanged -= TabsOnCollectionChanged;
            _viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
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
        _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        ApplyParametersPanelState();
    }
    
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        _viewModel.EditorHeadHeight = StackPanelEditor.Bounds.Height;
        _viewModel.ResultsHeaderHeight = StackPanelResult.Bounds.Height;
        _viewModel.ShowResultTool += ViewModelOnShowResultTool;
        _viewModel.Tabs.CollectionChanged += TabsOnCollectionChanged;
        ConfigureEditorContextMenu();
        UpdateActiveEditorState();
        ApplyResultsPanelState();
        ApplyParametersPanelState();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        CloseFunctionInsight();
        GetMainWindowViewModel()?.ClearActiveEditor(SqlEditor);
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

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabQueryEditorViewModel.HasDetectedParameters))
            ApplyParametersPanelState();
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

    private void ApplyParametersPanelState()
    {
        var splitterColumn = EditorLayoutGrid.ColumnDefinitions[1];
        var panelColumn = EditorLayoutGrid.ColumnDefinitions[2];

        if (_viewModel?.HasDetectedParameters == true)
        {
            splitterColumn.Width = new GridLength(5);
            panelColumn.MinWidth = MinimumParametersPanelWidth;
            panelColumn.Width = IsParametersPanelWidthCandidate(_previousParametersPanelWidth)
                ? _previousParametersPanelWidth
                : new GridLength(DefaultParametersPanelWidth);
            return;
        }

        if (panelColumn.ActualWidth > MinimumParametersPanelWidth)
            _previousParametersPanelWidth = new GridLength(panelColumn.ActualWidth);

        splitterColumn.Width = new GridLength(0);
        panelColumn.MinWidth = 0;
        panelColumn.Width = new GridLength(0);
    }

    private static bool IsParametersPanelWidthCandidate(GridLength width)
    {
        return !width.IsAbsolute || width.Value >= MinimumParametersPanelWidth;
    }

    private async void TextAreaOnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (e.Text is "(" or ",")
            ShowOrUpdateFunctionInsight();
        else if (e.Text == ")")
            CloseFunctionInsight();

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
        if (e.Text == "(")
            Dispatcher.UIThread.Post(ShowOrUpdateFunctionInsight, DispatcherPriority.Background);
    }

    private async void TextAreaOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space &&
            e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
            e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            ShowOrUpdateFunctionInsight();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var request = SqlCompletionProvider.GetManualCompletionRequest(SqlEditor.Text ?? string.Empty, SqlEditor.CaretOffset);
            await ShowCompletionAsync(request, rememberAsAutoRequest: false);
            e.Handled = true;
            return;
        }

        var mainWindowViewModel = GetMainWindowViewModel();
        if (mainWindowViewModel is null)
            return;

        UpdateActiveEditorState();

        if (TryHandleShortcut(e, mainWindowViewModel))
            e.Handled = true;
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

        CloseFunctionInsight();
        _completionWindow?.Close();
        var completionWindow = new CompletionWindow(SqlEditor.TextArea);
        _completionWindow = completionWindow;
        completionWindow.StartOffset = SqlCompletionProvider.GetCompletionStartOffset(SqlEditor.Text ?? string.Empty, SqlEditor.CaretOffset);

        var data = completionWindow.CompletionList.CompletionData;
        foreach (var completion in completions)
        {
            data.Add(completion);
        }

        completionWindow.Closed += (_, _) =>
        {
            if (ReferenceEquals(_completionWindow, completionWindow))
                _completionWindow = null;

            _completionInteractionState.ResetWhitespaceReopen();
            Dispatcher.UIThread.Post(ShowOrUpdateFunctionInsight, DispatcherPriority.Background);
        };
        completionWindow.Show();
    }

    private void ShowOrUpdateFunctionInsight()
    {
        if (_viewModel is null)
            return;

        if (_completionWindow is not null)
            return;

        var context = SqlFunctionCallContextDetector.Detect(SqlEditor.Text ?? string.Empty, SqlEditor.CaretOffset);
        if (context is null)
        {
            CloseFunctionInsight();
            return;
        }

        var function = SqlFunctionCatalog.FindFunction(_viewModel.ConnectionSettings.DatabaseType, context.FunctionName);
        if (function is null)
        {
            CloseFunctionInsight();
            return;
        }

        if (_functionInsightWindow is not null &&
            _functionOverloadProvider is not null &&
            string.Equals(_activeFunctionInsightName, function.Name, StringComparison.OrdinalIgnoreCase))
        {
            _functionOverloadProvider.UpdateArgumentIndex(context.ArgumentIndex);
            return;
        }

        CloseFunctionInsight();
        _functionOverloadProvider = new SqlFunctionOverloadProvider(function, context.ArgumentIndex);
        _activeFunctionInsightName = function.Name;
        _functionInsightWindow = new OverloadInsightWindow(SqlEditor.TextArea)
        {
            Provider = _functionOverloadProvider
        };
        _functionInsightWindow.Closed += (_, _) =>
        {
            _functionInsightWindow = null;
            _functionOverloadProvider = null;
            _activeFunctionInsightName = null;
        };
        _functionInsightWindow.Show();
    }

    private void CloseFunctionInsight()
    {
        _functionInsightWindow?.Close();
        _functionInsightWindow = null;
        _functionOverloadProvider = null;
        _activeFunctionInsightName = null;
    }

    private void SqlEditorOnGotFocus(object? sender, GotFocusEventArgs e)
    {
        UpdateActiveEditorState();
    }

    private void SqlEditorOnStateChanged(object? sender, EventArgs e)
    {
        GetMainWindowViewModel()?.RefreshActiveEditorState();
    }

    private void UpdateActiveEditorState()
    {
        if (_viewModel is null)
            return;

        GetMainWindowViewModel()?.SetActiveEditor(SqlEditor, _viewModel.ConnectionSettings.DatabaseType);
    }

    private MainWindowViewModel? GetMainWindowViewModel()
    {
        return this.TryGetParentWindow()?.DataContext as MainWindowViewModel;
    }

    private void ConfigureEditorContextMenu()
    {
        var mainWindowViewModel = GetMainWindowViewModel();
        if (mainWindowViewModel is null)
            return;

        var contextMenu = new ContextMenu();
        contextMenu.Opening += (_, _) => UpdateActiveEditorState();

        contextMenu.ItemsSource = BuildContextMenuItems(mainWindowViewModel);
        SqlEditor.ContextMenu = contextMenu;
    }

    private static IReadOnlyList<object> BuildContextMenuItems(MainWindowViewModel viewModel)
    {
        return
        [
            CreateMenuItem("Cu_t", viewModel.CutCommand, viewModel.CutGesture),
            CreateMenuItem("_Copy", viewModel.CopyCommand, viewModel.CopyGesture),
            CreateMenuItem("_Paste", viewModel.PasteCommand, viewModel.PasteGesture),
            new Separator(),
            CreateMenuItem("_Undo", viewModel.UndoCommand, viewModel.UndoGesture),
            CreateMenuItem("_Redo", viewModel.RedoCommand, viewModel.RedoGesture),
            new Separator(),
            CreateMenuItem("_Find", viewModel.FindCommand, viewModel.FindGesture),
            CreateMenuItem("_Replace", viewModel.ReplaceCommand, viewModel.ReplaceGesture),
            new Separator(),
            CreateMenuItem("UPPER", viewModel.UpperCommand, viewModel.UpperGesture),
            CreateMenuItem("lower", viewModel.LowerCommand, viewModel.LowerGesture),
            CreateMenuItem("_Beautify", viewModel.BeautifyCommand, viewModel.BeautifyGesture),
            new Separator(),
            CreateMenuItem("_Indent", viewModel.IndentCommand, viewModel.IndentGesture),
            CreateMenuItem("U_nindent", viewModel.UnindentCommand, viewModel.UnindentGesture),
            new Separator(),
            CreateMenuItem("_Comment", viewModel.CommentCommand, viewModel.CommentGesture),
            CreateMenuItem("U_ncomment", viewModel.UncommentCommand, viewModel.UncommentGesture)
        ];
    }

    private static MenuItem CreateMenuItem(string header, System.Windows.Input.ICommand command, KeyGesture inputGesture)
    {
        return new MenuItem
        {
            Header = header,
            Command = command,
            InputGesture = inputGesture
        };
    }

    private static bool TryHandleShortcut(KeyEventArgs e, MainWindowViewModel viewModel)
    {
        if (!viewModel.HasPrimaryShortcutModifier(e))
            return false;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.U && viewModel.HasSelection)
        {
            viewModel.UpperCommand.Execute().Subscribe();
            return true;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.K && viewModel.HasSelection)
        {
            viewModel.UncommentCommand.Execute().Subscribe();
            return true;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.Tab && viewModel.HasActiveEditor)
        {
            viewModel.UnindentCommand.Execute().Subscribe();
            return true;
        }

        return e.Key switch
        {
            Key.X when viewModel.CanCut => Execute(viewModel.CutCommand),
            Key.C when viewModel.CanCopy => Execute(viewModel.CopyCommand),
            Key.V when viewModel.CanPaste => Execute(viewModel.PasteCommand),
            Key.Z when viewModel.CanUndo => Execute(viewModel.UndoCommand),
            Key.Y when viewModel.CanRedo => Execute(viewModel.RedoCommand),
            Key.F when viewModel.HasActiveEditor => Execute(viewModel.FindCommand),
            Key.H when viewModel.HasActiveEditor => Execute(viewModel.ReplaceCommand),
            Key.U when viewModel.HasSelection => Execute(viewModel.LowerCommand),
            Key.B when viewModel.HasActiveEditor => Execute(viewModel.BeautifyCommand),
            Key.K when viewModel.HasSelection => Execute(viewModel.CommentCommand),
            Key.Tab when viewModel.HasActiveEditor => Execute(viewModel.IndentCommand),
            _ => false
        };
    }

    private static bool Execute(ReactiveCommand<Unit, Unit> command)
    {
        command.Execute().Subscribe();
        return true;
    }
}
