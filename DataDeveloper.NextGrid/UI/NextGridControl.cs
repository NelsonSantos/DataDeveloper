using System.Collections.ObjectModel;
using System.Linq;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace DataDeveloper.NextGrid.UI;

public sealed class NextGridControl : UserControl
{
    private const double ScrollBarSafetyMargin = 20;

    private readonly Grid _root;
    private readonly ScrollViewer _scrollViewer;
    private readonly NextGridPresenter _presenter;
    private readonly TextBox _editorTextBox;
    private readonly Popup _booleanPopup;
    private readonly ListBox _booleanListBox;
    private readonly List<BooleanPopupOption> _booleanOptions;
    private readonly Popup _dateTimePopup;
    private readonly NumericUpDown _dateDayUpDown;
    private readonly ComboBox _dateMonthComboBox;
    private readonly NumericUpDown _dateYearUpDown;
    private readonly NumericUpDown _timeHourUpDown;
    private readonly NumericUpDown _timeMinuteUpDown;
    private readonly NumericUpDown _timeSecondUpDown;
    private readonly IBrush _defaultEditorBorderBrush = new SolidColorBrush(Color.Parse("#434343"));
    private static readonly IBrush EditorErrorBorderBrush = new SolidColorBrush(Color.Parse("#C93C3C"));
    private bool _isEditorCommitInProgress;
    private bool _isSynchronizingDateTimeEditor;
    private bool _suppressDateTimePopupCancel;
    private Type? _activeDateTimeValueType;
    private object? _activeDateTimeOriginalValue;
    private bool _activeDateTimeAllowsNull;
    private readonly IReadOnlyList<string> _monthOptions =
    [
        "January",
        "February",
        "March",
        "April",
        "May",
        "June",
        "July",
        "August",
        "September",
        "October",
        "November",
        "December"
    ];

    public static readonly StyledProperty<ObservableCollection<string>> HeadersProperty =
        AvaloniaProperty.Register<NextGridControl, ObservableCollection<string>>(nameof(Headers), defaultValue: []);

    public static readonly StyledProperty<ObservableCollection<Type>> ColumnTypesProperty =
        AvaloniaProperty.Register<NextGridControl, ObservableCollection<Type>>(nameof(ColumnTypes), defaultValue: []);

    public static readonly StyledProperty<ObservableCollection<IReadOnlyList<object?>>> RowsProperty =
        AvaloniaProperty.Register<NextGridControl, ObservableCollection<IReadOnlyList<object?>>>(nameof(Rows), defaultValue: []);

    public static readonly StyledProperty<FontFamily> GridFontFamilyProperty =
        AvaloniaProperty.Register<NextGridControl, FontFamily>(nameof(GridFontFamily), defaultValue: new FontFamily("Consolas"));

    public static readonly StyledProperty<double> GridFontSizeProperty =
        AvaloniaProperty.Register<NextGridControl, double>(nameof(GridFontSize), defaultValue: 12d);

    public static readonly StyledProperty<IBrush> CellBackgroundProperty =
        AvaloniaProperty.Register<NextGridControl, IBrush>(nameof(CellBackground), defaultValue: Brushes.White);

    public static readonly StyledProperty<IBrush> CellForegroundProperty =
        AvaloniaProperty.Register<NextGridControl, IBrush>(nameof(CellForeground), defaultValue: Brushes.Black);

    public static readonly StyledProperty<IBrush> HeaderBackgroundProperty =
        AvaloniaProperty.Register<NextGridControl, IBrush>(nameof(HeaderBackground), defaultValue: Brushes.LightGray);

    public static readonly StyledProperty<IBrush> HeaderForegroundProperty =
        AvaloniaProperty.Register<NextGridControl, IBrush>(nameof(HeaderForeground), defaultValue: Brushes.Black);

    public static readonly StyledProperty<IBrush> GridLineProperty =
        AvaloniaProperty.Register<NextGridControl, IBrush>(nameof(GridLine), defaultValue: Brushes.Gray);

    public static readonly StyledProperty<IBrush> SelectionBackgroundProperty =
        AvaloniaProperty.Register<NextGridControl, IBrush>(nameof(SelectionBackground), defaultValue: Brushes.LightBlue);

    public static readonly StyledProperty<bool> CanEditCellsProperty =
        AvaloniaProperty.Register<NextGridControl, bool>(nameof(CanEditCells), defaultValue: false);

    public static readonly StyledProperty<ObservableCollection<int>> ReadOnlyColumnsProperty =
        AvaloniaProperty.Register<NextGridControl, ObservableCollection<int>>(nameof(ReadOnlyColumns), defaultValue: []);

    public static readonly StyledProperty<int> SelectedRowIndexProperty =
        AvaloniaProperty.Register<NextGridControl, int>(nameof(SelectedRowIndex), defaultValue: -1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string> SelectionStatusTextProperty =
        AvaloniaProperty.Register<NextGridControl, string>(
            nameof(SelectionStatusText),
            defaultValue: "Cell=nothing",
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public ObservableCollection<string> Headers
    {
        get => GetValue(HeadersProperty);
        set => SetValue(HeadersProperty, value);
    }

    public ObservableCollection<Type> ColumnTypes
    {
        get => GetValue(ColumnTypesProperty);
        set => SetValue(ColumnTypesProperty, value);
    }

    public ObservableCollection<IReadOnlyList<object?>> Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public FontFamily GridFontFamily
    {
        get => GetValue(GridFontFamilyProperty);
        set => SetValue(GridFontFamilyProperty, value);
    }

    public double GridFontSize
    {
        get => GetValue(GridFontSizeProperty);
        set => SetValue(GridFontSizeProperty, value);
    }

    public IBrush CellBackground
    {
        get => GetValue(CellBackgroundProperty);
        set => SetValue(CellBackgroundProperty, value);
    }

    public IBrush CellForeground
    {
        get => GetValue(CellForegroundProperty);
        set => SetValue(CellForegroundProperty, value);
    }

    public IBrush HeaderBackground
    {
        get => GetValue(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }

    public IBrush HeaderForeground
    {
        get => GetValue(HeaderForegroundProperty);
        set => SetValue(HeaderForegroundProperty, value);
    }

    public IBrush GridLine
    {
        get => GetValue(GridLineProperty);
        set => SetValue(GridLineProperty, value);
    }

    public IBrush SelectionBackground
    {
        get => GetValue(SelectionBackgroundProperty);
        set => SetValue(SelectionBackgroundProperty, value);
    }

    public bool CanEditCells
    {
        get => GetValue(CanEditCellsProperty);
        set => SetValue(CanEditCellsProperty, value);
    }

    public ObservableCollection<int> ReadOnlyColumns
    {
        get => GetValue(ReadOnlyColumnsProperty);
        set => SetValue(ReadOnlyColumnsProperty, value);
    }

    public int SelectedRowIndex
    {
        get => GetValue(SelectedRowIndexProperty);
        set => SetValue(SelectedRowIndexProperty, value);
    }

    public string SelectionStatusText
    {
        get => GetValue(SelectionStatusTextProperty);
        set => SetValue(SelectionStatusTextProperty, value);
    }

    public event EventHandler<GridCellEditCommittedEventArgs>? CellEditCommitted;

    public NextGridControl()
    {
        _presenter = new NextGridPresenter();
        _scrollViewer = new ScrollViewer
        {
            Content = _presenter,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            AllowAutoHide = false
        };
        _editorTextBox = new TextBox
        {
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            MinWidth = 40
        };
        _editorTextBox.KeyDown += OnEditorKeyDown;
        _editorTextBox.TextChanged += OnEditorTextChanged;
        _editorTextBox.LostFocus += OnEditorLostFocus;
        _editorTextBox.GotFocus += OnEditorGotFocus;
        _booleanOptions =
        [
            new BooleanPopupOption("True", true),
            new BooleanPopupOption("False", false),
            new BooleanPopupOption("Null", null)
        ];
        _booleanListBox = new ListBox
        {
            MinWidth = 88,
            ItemsSource = _booleanOptions,
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new FuncDataTemplate<BooleanPopupOption>((item, _) =>
                new TextBlock
                {
                    Text = item?.Text ?? string.Empty,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center
                }, true)
        };
        _booleanListBox.DoubleTapped += OnBooleanListBoxDoubleTapped;
        _booleanListBox.KeyDown += OnBooleanListBoxKeyDown;
        var booleanPopupContent = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#8A8A8A")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            Child = _booleanListBox
        };
        _booleanPopup = new Popup
        {
            IsLightDismissEnabled = true,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            PlacementTarget = _presenter,
            Child = booleanPopupContent
        };
        _booleanPopup.Closed += OnBooleanPopupClosed;
        _dateDayUpDown = CreateDateTimeNumericEditor(1, 31, 2);
        _dateMonthComboBox = new ComboBox
        {
            ItemsSource = _monthOptions,
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _dateYearUpDown = CreateDateTimeNumericEditor(1, 9999, 4);
        _timeHourUpDown = CreateDateTimeNumericEditor(0, 23, 2);
        _timeMinuteUpDown = CreateDateTimeNumericEditor(0, 59, 2);
        _timeSecondUpDown = CreateDateTimeNumericEditor(0, 59, 2);
        _dateDayUpDown.ValueChanged += OnDateTimePartValueChanged;
        _dateMonthComboBox.SelectionChanged += OnDateTimeMonthSelectionChanged;
        _dateYearUpDown.ValueChanged += OnDateTimePartValueChanged;
        _timeHourUpDown.ValueChanged += OnDateTimePartValueChanged;
        _timeMinuteUpDown.ValueChanged += OnDateTimePartValueChanged;
        _timeSecondUpDown.ValueChanged += OnDateTimePartValueChanged;
        _dateDayUpDown.AddHandler(InputElement.KeyDownEvent, OnDateTimePopupPreviewKeyDown, RoutingStrategies.Tunnel);
        _dateMonthComboBox.AddHandler(InputElement.KeyDownEvent, OnDateTimePopupPreviewKeyDown, RoutingStrategies.Tunnel);
        _dateYearUpDown.AddHandler(InputElement.KeyDownEvent, OnDateTimePopupPreviewKeyDown, RoutingStrategies.Tunnel);
        _timeHourUpDown.AddHandler(InputElement.KeyDownEvent, OnDateTimePopupPreviewKeyDown, RoutingStrategies.Tunnel);
        _timeMinuteUpDown.AddHandler(InputElement.KeyDownEvent, OnDateTimePopupPreviewKeyDown, RoutingStrategies.Tunnel);
        _timeSecondUpDown.AddHandler(InputElement.KeyDownEvent, OnDateTimePopupPreviewKeyDown, RoutingStrategies.Tunnel);
        var dateTimePopupContent = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#25272B")),
            BorderBrush = new SolidColorBrush(Color.Parse("#4A4D54")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            MinWidth = 336,
            Child = new StackPanel
            {
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 6,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Children =
                        {
                            _dateDayUpDown,
                            _dateMonthComboBox,
                            _dateYearUpDown
                        }
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 6,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Children =
                        {
                            _timeHourUpDown,
                            CreateDateTimeSeparator(":"),
                            _timeMinuteUpDown,
                            CreateDateTimeSeparator(":"),
                            _timeSecondUpDown
                        }
                    }
                }
            }
        };
        dateTimePopupContent.AddHandler(InputElement.KeyDownEvent, OnDateTimePopupPreviewKeyDown, RoutingStrategies.Tunnel);
        _dateTimePopup = new Popup
        {
            IsLightDismissEnabled = true,
            Placement = PlacementMode.Bottom,
            PlacementTarget = _presenter,
            Child = dateTimePopupContent
        };
        _dateTimePopup.Closed += OnDateTimePopupClosed;
        ScrollViewer.SetBringIntoViewOnFocusChange(_presenter, false);

        _root = new Grid();
        _root.Children.Add(_scrollViewer);
        _root.Children.Add(_editorTextBox);
        _root.Children.Add(_booleanPopup);
        _root.Children.Add(_dateTimePopup);

        Content = _root;

        _presenter.Bind(NextGridPresenter.HeadersProperty, this.GetBindingObservable(HeadersProperty));
        _presenter.Bind(NextGridPresenter.ColumnTypesProperty, this.GetBindingObservable(ColumnTypesProperty));
        _presenter.Bind(NextGridPresenter.RowsProperty, this.GetBindingObservable(RowsProperty));
        _presenter.Bind(NextGridPresenter.GridFontFamilyProperty, this.GetBindingObservable(GridFontFamilyProperty));
        _presenter.Bind(NextGridPresenter.GridFontSizeProperty, this.GetBindingObservable(GridFontSizeProperty));
        _presenter.Bind(NextGridPresenter.CellBackgroundProperty, this.GetBindingObservable(CellBackgroundProperty));
        _presenter.Bind(NextGridPresenter.CellForegroundProperty, this.GetBindingObservable(CellForegroundProperty));
        _presenter.Bind(NextGridPresenter.HeaderBackgroundProperty, this.GetBindingObservable(HeaderBackgroundProperty));
        _presenter.Bind(NextGridPresenter.HeaderForegroundProperty, this.GetBindingObservable(HeaderForegroundProperty));
        _presenter.Bind(NextGridPresenter.GridLineProperty, this.GetBindingObservable(GridLineProperty));
        _presenter.Bind(NextGridPresenter.SelectionBackgroundProperty, this.GetBindingObservable(SelectionBackgroundProperty));
        _presenter.Bind(NextGridPresenter.CanEditCellsProperty, this.GetBindingObservable(CanEditCellsProperty));
        _presenter.Bind(NextGridPresenter.ReadOnlyColumnsProperty, this.GetBindingObservable(ReadOnlyColumnsProperty));
        _presenter.AddHandler(Control.RequestBringIntoViewEvent, OnPresenterRequestBringIntoView, RoutingStrategies.Bubble);
        _presenter.PointerMoved += OnPresenterPointerMoved;
        _presenter.PointerExited += OnPresenterPointerExited;
        _scrollViewer.LayoutUpdated += OnScrollViewerLayoutUpdated;
        _presenter.FocusedCellChanged += OnPresenterFocusedCellChanged;
        _presenter.SelectionChanged += OnPresenterSelectionChanged;
        _presenter.CellEditRequested += OnPresenterCellEditRequested;
        _presenter.CellEditCommitted += OnPresenterCellEditCommitted;
        _presenter.CellEditCanceled += OnPresenterCellEditCanceled;
        _booleanListBox.Bind(TemplatedControl.FontFamilyProperty, this.GetBindingObservable(GridFontFamilyProperty));
        _booleanListBox.Bind(TemplatedControl.FontSizeProperty, this.GetBindingObservable(GridFontSizeProperty));
        _booleanListBox.Bind(ForegroundProperty, this.GetBindingObservable(CellForegroundProperty));
        _editorTextBox.Bind(TemplatedControl.FontFamilyProperty, this.GetBindingObservable(GridFontFamilyProperty));
        _editorTextBox.Bind(TemplatedControl.FontSizeProperty, this.GetBindingObservable(GridFontSizeProperty));
        _editorTextBox.Bind(ForegroundProperty, this.GetBindingObservable(CellForegroundProperty));
        _dateDayUpDown.Bind(TemplatedControl.FontFamilyProperty, this.GetBindingObservable(GridFontFamilyProperty));
        _dateDayUpDown.Bind(TemplatedControl.FontSizeProperty, this.GetBindingObservable(GridFontSizeProperty));
        _dateMonthComboBox.Bind(TemplatedControl.FontFamilyProperty, this.GetBindingObservable(GridFontFamilyProperty));
        _dateMonthComboBox.Bind(TemplatedControl.FontSizeProperty, this.GetBindingObservable(GridFontSizeProperty));
        _dateYearUpDown.Bind(TemplatedControl.FontFamilyProperty, this.GetBindingObservable(GridFontFamilyProperty));
        _dateYearUpDown.Bind(TemplatedControl.FontSizeProperty, this.GetBindingObservable(GridFontSizeProperty));
        _timeHourUpDown.Bind(TemplatedControl.FontFamilyProperty, this.GetBindingObservable(GridFontFamilyProperty));
        _timeHourUpDown.Bind(TemplatedControl.FontSizeProperty, this.GetBindingObservable(GridFontSizeProperty));
        _timeMinuteUpDown.Bind(TemplatedControl.FontFamilyProperty, this.GetBindingObservable(GridFontFamilyProperty));
        _timeMinuteUpDown.Bind(TemplatedControl.FontSizeProperty, this.GetBindingObservable(GridFontSizeProperty));
        _timeSecondUpDown.Bind(TemplatedControl.FontFamilyProperty, this.GetBindingObservable(GridFontFamilyProperty));
        _timeSecondUpDown.Bind(TemplatedControl.FontSizeProperty, this.GetBindingObservable(GridFontSizeProperty));
    }

    internal GridCellAddress? GetFocusedCellForTest() => _presenter.GetFocusedCellForTest();

    internal int GetVisibleRowCountForTest() => _presenter.GetVisibleRowCountForTest();

    internal GridCellBounds GetCellBoundsForTest(int rowIndex, int columnIndex) =>
        _presenter.GetCellBoundsForTest(rowIndex, columnIndex);

    internal Point GetCellClickPointForTest(int rowIndex, int columnIndex) =>
        _presenter.GetCellClickPointForTest(rowIndex, columnIndex);

    internal void SelectCellAtLocalPointForTest(Point point) =>
        SelectCellAtLocalPointInternal(point);

    internal GridHitTestResult HitTestAtLocalPointForTest(Point point) =>
        _presenter.HitTestAtLocalPointForTest(point);

    internal void DragSelectCellsForTest(GridCellAddress start, GridCellAddress end) =>
        DragSelectCellsInternal(start, end);

    internal bool SelectionContainsForTest(GridCellAddress cell) =>
        _presenter.SelectionContainsForTest(cell);

    internal int GetSelectedRowIndexForTest() => SelectedRowIndex;

    internal void BeginEditFocusedCellForTest() => _presenter.BeginEditAtFocusedCell();

    internal void CommitEditForTest(string value)
    {
        _editorTextBox.Text = value;
        CommitEditor();
    }

    internal void CommitBooleanEditForTest(bool? value)
    {
        ShowBooleanPopupForTest();
        CommitBooleanSelection(value);
    }

    internal void CommitDateTimeEditForTest(DateTime value)
    {
        SetDateTimePopupParts(new DateTimeOffset(value));
        CommitDateTimeSelection();
    }

    private void SelectCellAtLocalPointInternal(Point point)
    {
        _presenter.SelectCellAtLocalPointForTest(point);
        SelectedRowIndex = _presenter.GetFocusedCellForTest()?.Row ?? -1;
    }

    private void DragSelectCellsInternal(GridCellAddress start, GridCellAddress end)
    {
        _presenter.DragSelectCellsForTest(start, end);
        SelectedRowIndex = _presenter.GetFocusedCellForTest()?.Row ?? -1;
    }

    private void OnPresenterRequestBringIntoView(object? sender, RequestBringIntoViewEventArgs e)
    {
        if (ReferenceEquals(e.TargetObject, _presenter))
            e.Handled = true;
    }

    private void OnScrollViewerLayoutUpdated(object? sender, EventArgs e)
    {
        var horizontalReserve = 0d;
        var verticalReserve = 0d;

        foreach (var scrollBar in _scrollViewer.GetVisualDescendants().OfType<ScrollBar>())
        {
            if (!scrollBar.IsVisible)
                continue;

            if (scrollBar.Orientation == Orientation.Horizontal)
                horizontalReserve = Math.Max(horizontalReserve, scrollBar.Bounds.Height);
            else if (scrollBar.Orientation == Orientation.Vertical)
                verticalReserve = Math.Max(verticalReserve, scrollBar.Bounds.Width);
        }

        _presenter.SetScrollBarReserve(
            horizontalReserve > 0 ? horizontalReserve + ScrollBarSafetyMargin : 0,
            verticalReserve > 0 ? verticalReserve + ScrollBarSafetyMargin : 0);
    }

    private void OnPresenterFocusedCellChanged(object? sender, GridFocusedCellChangedEventArgs e)
    {
        SelectedRowIndex = e.Cell?.Row ?? -1;
    }

    private void OnPresenterSelectionChanged(object? sender, GridSelectionChangedEventArgs e)
    {
        SelectionStatusText = GridSelectionStatusFormatter.Format(e.FocusCell, e.Ranges);
    }

    private void OnPresenterPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_editorTextBox.IsVisible)
            return;

        var hit = _presenter.HitTestAtLocalPointForTest(e.GetPosition(_presenter));
        if (hit.Region == GridRegionKind.Cell &&
            hit.RowIndex >= 0 &&
            hit.RowIndex < Rows.Count &&
            Rows[hit.RowIndex] is IGridEditableRow editableRow)
        {
            var message = editableRow.GetValidationError(hit.ColumnIndex);
            ToolTip.SetTip(_presenter, message);
            return;
        }

        ToolTip.SetTip(_presenter, null);
    }

    private void OnPresenterPointerExited(object? sender, PointerEventArgs e)
    {
        ToolTip.SetTip(_presenter, null);
    }

    private void OnPresenterCellEditRequested(object? sender, GridCellEditRequestedEventArgs e)
    {
        ClearEditorError();
        if (IsBooleanEditor(e.Session))
        {
            ShowBooleanPopup(e.Session, e.Bounds);
            return;
        }

        if (IsDateTimeEditor(e.Session))
        {
            _editorTextBox.Text = e.Session.CurrentValue?.ToString() ?? string.Empty;
            _editorTextBox.Width = Math.Max(160, e.Bounds.Width - 2);
            _editorTextBox.Height = Math.Max(24, e.Bounds.Height - 2);
            _editorTextBox.Margin = new Thickness(e.Bounds.X + 1, e.Bounds.Y + 1, 0, 0);
            _editorTextBox.IsVisible = true;
            ShowDateTimePopup(e.Session, e.Bounds);
            return;
        }

        _editorTextBox.Text = e.Session.CurrentValue?.ToString() ?? string.Empty;
        _editorTextBox.Width = Math.Max(40, e.Bounds.Width - 2);
        _editorTextBox.Height = Math.Max(24, e.Bounds.Height - 2);
        _editorTextBox.Margin = new Thickness(e.Bounds.X + 1, e.Bounds.Y + 1, 0, 0);
        _editorTextBox.IsVisible = true;
        _editorTextBox.Focus();
        _editorTextBox.SelectAll();
    }

    private void OnPresenterCellEditCommitted(object? sender, GridCellEditCommittedEventArgs e)
    {
        HideEditor();
        ApplyCommittedValue(e.Result);
        _presenter.Focus();
        CellEditCommitted?.Invoke(this, e);
    }

    private void OnPresenterCellEditCanceled(object? sender, EventArgs e)
    {
        HideEditor();
        _presenter.Focus();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_editorTextBox.IsVisible)
            return;

        if (e.Key == Key.Enter)
        {
            TryCompleteEditor(cancelOnFailure: false);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            _presenter.CancelEdit();
            e.Handled = true;
        }
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        ClearEditorError();
        if (IsDateTimeEditorActive())
            SyncDateTimePopupFromEditorText();
    }

    private void OnEditorGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (!IsDateTimeEditorActive() || !_dateTimePopup.IsOpen)
            return;

        CloseDateTimePopup(cancelEdit: false);
    }

    private void OnEditorLostFocus(object? sender, RoutedEventArgs e)
    {
        if (IsDateTimeEditorActive())
            return;

        if (_editorTextBox.IsVisible && !_isEditorCommitInProgress)
            TryCompleteEditor(cancelOnFailure: true);
    }

    private void CommitEditor()
    {
        TryCompleteEditor(cancelOnFailure: false);
    }

    private void TryCompleteEditor(bool cancelOnFailure)
    {
        if (!_editorTextBox.IsVisible)
            return;

        try
        {
            _isEditorCommitInProgress = true;
            _presenter.CommitEdit(_editorTextBox.Text);
        }
        catch (FormatException ex)
        {
            if (cancelOnFailure)
            {
                _presenter.CancelEdit();
                return;
            }

            ShowEditorError(ex.Message);
            _editorTextBox.Focus();
            _editorTextBox.SelectAll();
        }
        finally
        {
            _isEditorCommitInProgress = false;
        }
    }

    private void HideEditor()
    {
        ClearEditorError();
        _editorTextBox.IsVisible = false;
        _editorTextBox.Margin = new Thickness(0);
        _booleanPopup.IsOpen = false;
        _suppressDateTimePopupCancel = true;
        _dateTimePopup.IsOpen = false;
        _suppressDateTimePopupCancel = false;
        _isSynchronizingDateTimeEditor = false;
        _activeDateTimeValueType = null;
        _activeDateTimeOriginalValue = null;
    }

    private void ShowEditorError(string message)
    {
        _editorTextBox.BorderBrush = EditorErrorBorderBrush;
        ToolTip.SetTip(_editorTextBox, message);
    }

    private void ClearEditorError()
    {
        _editorTextBox.BorderBrush = _defaultEditorBorderBrush;
        ToolTip.SetTip(_editorTextBox, null);
    }

    private static bool IsBooleanEditor(DataDeveloper.NextGrid.Editors.GridEditSession session)
    {
        var type = Nullable.GetUnderlyingType(session.ValueType ?? session.OriginalValue?.GetType() ?? typeof(object))
                   ?? session.ValueType
                   ?? session.OriginalValue?.GetType();
        return type == typeof(bool);
    }

    private static bool IsDateTimeEditor(DataDeveloper.NextGrid.Editors.GridEditSession session)
    {
        var type = Nullable.GetUnderlyingType(session.ValueType ?? session.OriginalValue?.GetType() ?? typeof(object))
                   ?? session.ValueType
                   ?? session.OriginalValue?.GetType();
        return type == typeof(DateTime) || type == typeof(DateTimeOffset);
    }

    private void ShowBooleanPopup(DataDeveloper.NextGrid.Editors.GridEditSession session, GridCellBounds bounds)
    {
        var allowNull = session.ValueType is null || Nullable.GetUnderlyingType(session.ValueType) is not null;
        _booleanPopup.HorizontalOffset = bounds.X + 1;
        _booleanPopup.VerticalOffset = bounds.Y + bounds.Height + 1;
        _booleanPopup.IsOpen = true;

        _booleanOptions[2].IsVisible = allowNull;
        _booleanListBox.ItemsSource = null;
        _booleanListBox.ItemsSource = _booleanOptions.Where(option => option.IsVisible).ToList();

        var selectedOption = session.CurrentValue switch
        {
            true => _booleanOptions[0],
            false => _booleanOptions[1],
            _ => allowNull ? _booleanOptions[2] : _booleanOptions[0]
        };

        _booleanListBox.SelectedItem = selectedOption;
        Dispatcher.UIThread.Post(() =>
        {
            _booleanListBox.Focus();
            (_booleanListBox.ContainerFromItem(selectedOption) as Control)?.Focus();
        }, DispatcherPriority.Input);
    }

    private void ShowDateTimePopup(DataDeveloper.NextGrid.Editors.GridEditSession session, GridCellBounds bounds)
    {
        _activeDateTimeValueType = session.ValueType;
        _activeDateTimeOriginalValue = session.OriginalValue;
        _activeDateTimeAllowsNull = session.ValueType is null || Nullable.GetUnderlyingType(session.ValueType) is not null;

        var initial = ResolveDateTimePopupInitialValue(session);

        SetDateTimePopupParts(initial);
        PositionDateTimePopup(bounds);
        _dateTimePopup.IsOpen = true;

        Dispatcher.UIThread.Post(() =>
        {
            _dateDayUpDown.Focus();
        }, DispatcherPriority.Input);
    }

    internal static DateTimeOffset ResolveDateTimePopupInitialValue(DataDeveloper.NextGrid.Editors.GridEditSession session)
    {
        if (TryCoerceDateTimeOffset(session.OriginalValue, out var originalValue))
            return originalValue;

        if (TryCoerceDateTimeOffset(session.CurrentValue, out var currentValue))
            return currentValue;

        return DateTimeOffset.Now;
    }

    private static bool TryCoerceDateTimeOffset(object? value, out DateTimeOffset result)
    {
        switch (value)
        {
            case DateTimeOffset dateTimeOffset:
                result = dateTimeOffset;
                return true;
            case DateTime dateTime:
                result = new DateTimeOffset(dateTime);
                return true;
            case string text when !string.IsNullOrWhiteSpace(text):
                if (GridValueFormats.TryParseDateTimeOffset(text, out var parsedOffset))
                {
                    result = parsedOffset;
                    return true;
                }

                if (GridValueFormats.TryParseDateTime(text, out var parsedDateTime))
                {
                    result = new DateTimeOffset(parsedDateTime);
                    return true;
                }

                break;
        }

        result = default;
        return false;
    }

    private void CommitBooleanSelection(bool? value)
    {
        try
        {
            _isEditorCommitInProgress = true;
            _presenter.CommitEdit(value);
        }
        finally
        {
            _isEditorCommitInProgress = false;
        }
    }

    private void OnBooleanPopupClosed(object? sender, EventArgs e)
    {
        if (_isEditorCommitInProgress || _presenter is null)
            return;

        _presenter.CancelEdit();
    }

    private void OnBooleanListBoxDoubleTapped(object? sender, TappedEventArgs e)
    {
        CommitBooleanSelectionFromListBox();
    }

    private void OnBooleanListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _presenter.CancelEdit();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
            return;

        CommitBooleanSelectionFromListBox();
        e.Handled = true;
    }

    private void ShowBooleanPopupForTest()
    {
        _booleanPopup.IsOpen = true;
    }

    private void CommitBooleanSelectionFromListBox()
    {
        if (_booleanListBox.SelectedItem is BooleanPopupOption option)
            CommitBooleanSelection(option.Value);
    }

    private void OnDateTimeEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseDateTimePopup(cancelEdit: false);
            _editorTextBox.Focus();
            _editorTextBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
            return;

        CommitDateTimeSelection();
        e.Handled = true;
    }

    private void OnDateTimePopupPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_dateTimePopup.IsOpen)
            return;

        if (e.Key == Key.Escape)
        {
            CloseDateTimePopup(cancelEdit: false);
            _editorTextBox.Focus();
            _editorTextBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
            return;

        CommitDateTimeSelection();
        e.Handled = true;
    }

    private void OnDateTimePartValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (!IsDateTimeEditorActive() || _isSynchronizingDateTimeEditor)
            return;

        if (!TryBuildDateTimeValue(out var value, out _))
            return;

        UpdateDateTimeEditorText(value);
    }

    private void OnDateTimeMonthSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!IsDateTimeEditorActive() || _isSynchronizingDateTimeEditor)
            return;

        if (!TryBuildDateTimeValue(out var value, out _))
            return;

        UpdateDateTimeEditorText(value);
    }

    private void OnDateTimePopupClosed(object? sender, EventArgs e)
    {
        if (_suppressDateTimePopupCancel)
            return;

        if (_isEditorCommitInProgress || _presenter is null)
            return;

        if (_editorTextBox.IsVisible && _editorTextBox.IsKeyboardFocusWithin)
            return;

        _presenter.CancelEdit();
    }

    private void CommitDateTimeSelection()
    {
        if (!TryBuildDateTimeValue(out var value, out var errorMessage))
        {
            ShowEditorError(errorMessage);
            _dateDayUpDown.Focus();
            return;
        }

        try
        {
            _isEditorCommitInProgress = true;
            _presenter.CommitEdit(value);
        }
        finally
        {
            _isEditorCommitInProgress = false;
        }
    }

    private bool TryBuildDateTimeValue(out object value, out string errorMessage)
    {
        value = string.Empty;
        errorMessage = string.Empty;

        var day = (int?)_dateDayUpDown.Value;
        var monthIndex = _dateMonthComboBox.SelectedIndex;
        var year = (int?)_dateYearUpDown.Value;
        var hour = (int?)_timeHourUpDown.Value;
        var minute = (int?)_timeMinuteUpDown.Value;
        var second = (int?)_timeSecondUpDown.Value;

        if (day is null || monthIndex < 0 || year is null || hour is null || minute is null || second is null)
        {
            errorMessage = "Date and time are required.";
            return false;
        }

        DateTime date;
        try
        {
            date = new DateTime(year.Value, monthIndex + 1, day.Value, hour.Value, minute.Value, second.Value);
        }
        catch (ArgumentOutOfRangeException)
        {
            errorMessage = "Invalid date/time value.";
            return false;
        }

        var targetType = Nullable.GetUnderlyingType(_activeDateTimeValueType ?? typeof(DateTime)) ?? _activeDateTimeValueType ?? typeof(DateTime);
        if (targetType == typeof(DateTimeOffset))
        {
            var originalOffset = _activeDateTimeOriginalValue as DateTimeOffset? ?? DateTimeOffset.Now;
            value = new DateTimeOffset(date, originalOffset.Offset);
            return true;
        }

        value = date;
        return true;
    }

    private bool IsDateTimeEditorActive()
    {
        return _editorTextBox.IsVisible &&
               _dateTimePopup.IsOpen &&
               _activeDateTimeValueType is not null;
    }

    private void SyncDateTimePopupFromEditorText()
    {
        if (_isSynchronizingDateTimeEditor)
            return;

        var text = _editorTextBox.Text;
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (!GridValueFormats.TryParseDateTimeOffset(text, out var dateTimeOffset))
        {
            if (!GridValueFormats.TryParseDateTime(text, out var dateTime))
                return;

            dateTimeOffset = new DateTimeOffset(dateTime);
        }

        SetDateTimePopupParts(dateTimeOffset);
    }

    private void UpdateDateTimeEditorText(object value)
    {
        _isSynchronizingDateTimeEditor = true;
        _editorTextBox.Text = value switch
        {
            DateTime dateTime => GridValueFormats.FormatDateTime(dateTime),
            DateTimeOffset dateTimeOffset => GridValueFormats.FormatDateTimeOffset(dateTimeOffset),
            _ => value.ToString() ?? string.Empty
        };
        _isSynchronizingDateTimeEditor = false;
    }

    private void PositionDateTimePopup(GridCellBounds bounds)
    {
        const double estimatedPopupHeight = 124;
        _dateTimePopup.PlacementRect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        _dateTimePopup.HorizontalOffset = 0;
        _dateTimePopup.VerticalOffset = 2;
        var below = bounds.Y + bounds.Height + estimatedPopupHeight <= _presenter.Bounds.Height;
        _dateTimePopup.Placement = below || bounds.Y <= estimatedPopupHeight
            ? PlacementMode.Bottom
            : PlacementMode.Top;
    }

    private void CloseDateTimePopup(bool cancelEdit)
    {
        if (!_dateTimePopup.IsOpen)
            return;

        _suppressDateTimePopupCancel = !cancelEdit;
        _dateTimePopup.IsOpen = false;
        _suppressDateTimePopupCancel = false;

        if (cancelEdit)
            _presenter.CancelEdit();
    }

    private void SetDateTimePopupParts(DateTimeOffset value)
    {
        _isSynchronizingDateTimeEditor = true;
        _dateDayUpDown.Value = value.Day;
        _dateMonthComboBox.SelectedIndex = value.Month - 1;
        _dateYearUpDown.Value = value.Year;
        _timeHourUpDown.Value = value.Hour;
        _timeMinuteUpDown.Value = value.Minute;
        _timeSecondUpDown.Value = value.Second;
        _isSynchronizingDateTimeEditor = false;
    }

    private static NumericUpDown CreateDateTimeNumericEditor(decimal min, decimal max, int digits)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Increment = 1,
            AllowSpin = true,
            ClipValueToMinMax = true,
            ShowButtonSpinner = true,
            FormatString = "F0",
            HorizontalContentAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            MinWidth = digits >= 4 ? 84 : 56
        };
    }

    private static Control CreateDateTimeSeparator(string text)
    {
        return new Border
        {
            Width = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
    }

    private sealed class BooleanPopupOption(string text, bool? value)
    {
        public string Text { get; } = text;
        public bool? Value { get; } = value;
        public bool IsVisible { get; set; } = true;
        public override string ToString() => Text;
    }

    private void ApplyCommittedValue(DataDeveloper.NextGrid.Editors.GridEditResult result)
    {
        if (result.Cell.Row < 0 || result.Cell.Row >= Rows.Count)
            return;

        switch (Rows[result.Cell.Row])
        {
            case IGridEditableRow editableRow:
                editableRow.SetValue(result.Cell.Column, result.NewValue);
                Rows[result.Cell.Row] = editableRow;
                break;
            case object?[] array:
                array[result.Cell.Column] = result.NewValue;
                Rows[result.Cell.Row] = array;
                break;
        }
    }
}
