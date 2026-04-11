using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DataDeveloper.NextGrid.Clipboard;
using DataDeveloper.NextGrid.Editors;
using DataDeveloper.NextGrid.Renderers;

namespace DataDeveloper.NextGrid.UI;

internal sealed class NextGridPresenter : Control, IScrollable, ILogicalScrollable
{
    private const double DefaultColumnWidth = 120;
    private const double HeaderHeight = 34;
    private const double RowHeight = 28;
    private const double ResizeHandleHalfWidth = 4;
    private static readonly IBrush ValidationErrorBrush = new SolidColorBrush(Color.Parse("#C93C3C"));
    private static readonly Pen ValidationErrorPen = new(ValidationErrorBrush, 1.5);

    private readonly GridColumnLayoutEngine _columnLayout = new(DefaultColumnWidth);
    private readonly GridTableController _tableController;
    private readonly GridEditorHost _editorHost;
    private readonly GridRendererRegistry _rendererRegistry = new();
    private readonly NextGridClipboardBuilder _clipboardBuilder;
    private readonly RowRenderCache _rowRenderCache;
    private Vector _offset;
    private double _horizontalScrollBarReserve;
    private double _verticalScrollBarReserve;
    private bool _autoWidthPending = true;
    private readonly List<IGridCellRenderer?> _columnRenderers = [];
    private int? _resizingColumnIndex;
    private double _resizeAnchorX;
    private double _resizeOriginalWidth;
    private double _lastMeasuredRowHeaderWidth = 44;
    private bool _isSelecting;
    private GridRegionKind _selectionOriginRegion;
    private bool _isRefreshScheduled;
    private bool _pendingScrollInvalidation;
    private bool _pendingMeasureInvalidation;
    private bool _pendingVisualInvalidation;
    private ObservableCollection<int>? _readOnlyColumnsSubscription;

    private ObservableCollection<string>? _headersSubscription;
    private ObservableCollection<Type>? _typesSubscription;
    private ObservableCollection<IReadOnlyList<object?>>? _rowsSubscription;

    public static readonly StyledProperty<ObservableCollection<string>> HeadersProperty =
        AvaloniaProperty.Register<NextGridPresenter, ObservableCollection<string>>(nameof(Headers), defaultValue: []);

    public static readonly StyledProperty<ObservableCollection<Type>> ColumnTypesProperty =
        AvaloniaProperty.Register<NextGridPresenter, ObservableCollection<Type>>(nameof(ColumnTypes), defaultValue: []);

    public static readonly StyledProperty<ObservableCollection<IReadOnlyList<object?>>> RowsProperty =
        AvaloniaProperty.Register<NextGridPresenter, ObservableCollection<IReadOnlyList<object?>>>(nameof(Rows), defaultValue: []);

    public static readonly StyledProperty<FontFamily> GridFontFamilyProperty =
        AvaloniaProperty.Register<NextGridPresenter, FontFamily>(nameof(GridFontFamily), defaultValue: new FontFamily("Consolas"));

    public static readonly StyledProperty<double> GridFontSizeProperty =
        AvaloniaProperty.Register<NextGridPresenter, double>(nameof(GridFontSize), defaultValue: 12d);

    public static readonly StyledProperty<IBrush> CellBackgroundProperty =
        AvaloniaProperty.Register<NextGridPresenter, IBrush>(nameof(CellBackground), defaultValue: Brushes.White);

    public static readonly StyledProperty<IBrush> CellForegroundProperty =
        AvaloniaProperty.Register<NextGridPresenter, IBrush>(nameof(CellForeground), defaultValue: Brushes.Black);

    public static readonly StyledProperty<IBrush> HeaderBackgroundProperty =
        AvaloniaProperty.Register<NextGridPresenter, IBrush>(nameof(HeaderBackground), defaultValue: Brushes.LightGray);

    public static readonly StyledProperty<IBrush> HeaderForegroundProperty =
        AvaloniaProperty.Register<NextGridPresenter, IBrush>(nameof(HeaderForeground), defaultValue: Brushes.Black);

    public static readonly StyledProperty<IBrush> GridLineProperty =
        AvaloniaProperty.Register<NextGridPresenter, IBrush>(nameof(GridLine), defaultValue: Brushes.Gray);

    public static readonly StyledProperty<IBrush> SelectionBackgroundProperty =
        AvaloniaProperty.Register<NextGridPresenter, IBrush>(nameof(SelectionBackground), defaultValue: Brushes.LightBlue);

    public static readonly StyledProperty<bool> CanEditCellsProperty =
        AvaloniaProperty.Register<NextGridPresenter, bool>(nameof(CanEditCells), defaultValue: false);

    public static readonly StyledProperty<ObservableCollection<int>> ReadOnlyColumnsProperty =
        AvaloniaProperty.Register<NextGridPresenter, ObservableCollection<int>>(nameof(ReadOnlyColumns), defaultValue: []);

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

    public Size Extent => new(
        GetRowHeaderWidth() + _columnLayout.GetTotalWidth() + _verticalScrollBarReserve,
        HeaderHeight + (Rows.Count * RowHeight) + _horizontalScrollBarReserve);

    public Vector Offset
    {
        get => _offset;
        set
        {
            var clamped = ClampOffset(value);
            if (_offset == clamped)
                return;

            _offset = clamped;
            RequestRefresh(scroll: true, visual: true);
        }
    }

    public Size Viewport => new Size(
        Math.Max(0, Bounds.Width),
        Math.Max(0, Bounds.Height));

    public bool CanHorizontallyScroll { get; set; } = true;

    public bool CanVerticallyScroll { get; set; } = true;

    public bool IsLogicalScrollEnabled => true;

    public Size ScrollSize => new(DefaultColumnWidth, RowHeight);

    public Size PageScrollSize => new(
        Math.Max(DefaultColumnWidth, Viewport.Width - GetRowHeaderWidth()),
        Math.Max(RowHeight, Viewport.Height - HeaderHeight));

    public event EventHandler? ScrollInvalidated;
    public event EventHandler<GridFocusedCellChangedEventArgs>? FocusedCellChanged;
    public event EventHandler<GridSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<GridCellEditRequestedEventArgs>? CellEditRequested;
    public event EventHandler<GridCellEditCommittedEventArgs>? CellEditCommitted;
    public event EventHandler? CellEditCanceled;

    public NextGridPresenter()
    {
        var layoutEngine = new GridLayoutEngine(_columnLayout);
        var viewportEngine = new GridViewportEngine(_columnLayout);
        var selection = new GridSelectionModel();
        _tableController = new GridTableController(_columnLayout, layoutEngine, viewportEngine, selection);
        _editorHost = new GridEditorHost(new GridEditorRegistry());
        _clipboardBuilder = new NextGridClipboardBuilder(_rendererRegistry);
        _rowRenderCache = new RowRenderCache();
        Focusable = true;
        PropertyChanged += OnControlPropertyChanged;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerExited += OnPointerExited;
        DoubleTapped += OnDoubleTapped;
        KeyDown += OnKeyDown;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Headers.Count == 0)
            return;

        _columnLayout.EnsureColumnCount(Headers.Count);
        InitializeColumnWidthsIfNeeded();
        _tableController.SetDimensions(Rows.Count, Headers.Count);
        UpdateControllerViewport();

        var rowRange = _tableController.GetRowRenderRange();
        var columnRange = _tableController.GetColumnRenderRange();
        _rowRenderCache.EnsureWindow(
            _tableController.TopRowIndex,
            Rows.Count,
            Headers.Count,
            Rows,
            ColumnTypes,
            GetTypeface(),
            GridFontSize,
            CellForeground,
            GridRendererContext.Default,
            GetColumnRenderer);
        var viewportInfo = new GridViewportInfo(
            rowRange,
            columnRange,
            _tableController.State.HorizontalOffset,
            _tableController.State.VerticalOffset);

        DrawHeaders(context, columnRange);
        DrawRowHeaders(context, rowRange);
        DrawCells(context, viewportInfo);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _columnLayout.EnsureColumnCount(Headers.Count);
        InitializeColumnWidthsIfNeeded();
        return Extent;
    }

    public bool BringIntoView(Control target, Rect targetRect)
    {
        if (!ReferenceEquals(target, this))
            return false;

        var nextContentOffset = new Vector(GetContentOffsetX(), GetContentOffsetY());
        var contentViewportWidth = Math.Max(0, Viewport.Width - GetRowHeaderWidth());
        var contentViewportHeight = Math.Max(0, Viewport.Height - HeaderHeight);

        if (targetRect.X < nextContentOffset.X)
            nextContentOffset = new Vector(targetRect.X, nextContentOffset.Y);
        else if (targetRect.Right > nextContentOffset.X + contentViewportWidth)
            nextContentOffset = new Vector(targetRect.Right - contentViewportWidth, nextContentOffset.Y);

        if (targetRect.Y < nextContentOffset.Y)
            nextContentOffset = new Vector(nextContentOffset.X, targetRect.Y);
        else if (targetRect.Bottom > nextContentOffset.Y + contentViewportHeight)
            nextContentOffset = new Vector(nextContentOffset.X, targetRect.Bottom - contentViewportHeight);

        var nextOffset = ToScrollViewerOffset(nextContentOffset.X, nextContentOffset.Y);
        var changed = nextOffset != Offset;
        Offset = nextOffset;
        return changed;
    }

    public Control? GetControlInDirection(NavigationDirection direction, Control? from)
    {
        return null;
    }

    public void RaiseScrollInvalidated(EventArgs e)
    {
        ScrollInvalidated?.Invoke(this, e);
    }

    private void OnControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == HeadersProperty)
        {
            ReplaceSubscription(ref _headersSubscription, Headers, OnCollectionChanged);
            _autoWidthPending = true;
            _columnRenderers.Clear();
            _rowRenderCache.Clear();
        }

        if (e.Property == ColumnTypesProperty)
        {
            ReplaceSubscription(ref _typesSubscription, ColumnTypes, OnCollectionChanged);
            _autoWidthPending = true;
            _columnRenderers.Clear();
            _rowRenderCache.Clear();
        }

        if (e.Property == RowsProperty)
        {
            ReplaceSubscription(ref _rowsSubscription, Rows, OnCollectionChanged);
            _autoWidthPending = true;
            _columnRenderers.Clear();
            _rowRenderCache.Clear();
        }

        if (e.Property == ReadOnlyColumnsProperty)
            ReplaceSubscription(ref _readOnlyColumnsSubscription, ReadOnlyColumns, OnCollectionChanged);

        if (e.Property == GridFontFamilyProperty ||
            e.Property == GridFontSizeProperty ||
            e.Property == CellBackgroundProperty ||
            e.Property == CellForegroundProperty ||
            e.Property == HeaderBackgroundProperty ||
            e.Property == HeaderForegroundProperty ||
            e.Property == GridLineProperty ||
            e.Property == SelectionBackgroundProperty)
        {
            _autoWidthPending = true;
            _rowRenderCache.Clear();
            RequestRefresh(measure: true, visual: true);
        }

        if (e.Property == BoundsProperty)
        {
            Offset = ClampOffset(Offset);
            RequestRefresh(scroll: true, visual: true);
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var isRowsChange = ReferenceEquals(sender, Rows);
        var shouldInvalidateVisual = true;

        if (isRowsChange)
        {
            _tableController.SetDimensions(Rows.Count, Headers.Count);
            UpdateControllerViewport();
        }

        if (ReferenceEquals(sender, Rows))
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null && !_autoWidthPending)
            {
                TrackAddedRowWidths(e.NewItems);
                shouldInvalidateVisual = ShouldInvalidateVisualForAddedRows(e);
            }
            else
            {
                _autoWidthPending = true;
                _rowRenderCache.Clear();
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
            _columnRenderers.Clear();

        var rowHeaderWidthChanged = Math.Abs(GetRowHeaderWidth() - _lastMeasuredRowHeaderWidth) > 0.01d;
        Offset = ClampOffset(Offset);
        RequestRefresh(scroll: true);

        if (e.Action == NotifyCollectionChangedAction.Reset || rowHeaderWidthChanged)
            RequestRefresh(measure: true);

        if (shouldInvalidateVisual || rowHeaderWidthChanged || !isRowsChange)
            RequestRefresh(visual: true);
    }

    private bool ShouldInvalidateVisualForAddedRows(NotifyCollectionChangedEventArgs e)
    {
        if (e.NewStartingIndex < 0)
            return true;

        var visibleRows = _tableController.GetRowRenderRange();
        return e.NewStartingIndex < visibleRows.EndExclusive;
    }

    private void TrackAddedRowWidths(System.Collections.IList newItems)
    {
        _columnLayout.EnsureColumnCount(Headers.Count);

        foreach (var item in newItems)
        {
            if (item is not IReadOnlyList<object?> row)
                continue;

            for (var columnIndex = 0; columnIndex < Headers.Count; columnIndex++)
            {
                var value = columnIndex < row.Count ? row[columnIndex] : null;
                var renderer = GetColumnRenderer(columnIndex, value);
                var width = renderer.MeasureWidth(value, GridRendererContext.Default, text => MeasureTextWidth(text, CellForeground)) + 16;
                _columnLayout.TrackWidth(columnIndex, width);
            }
        }

        _lastMeasuredRowHeaderWidth = GetRowHeaderWidth();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Headers.Count == 0 || Rows.Count == 0)
            return;

        var point = e.GetPosition(this);
        _tableController.SetDimensions(Rows.Count, Headers.Count);
        UpdateControllerViewport();

        if (TryGetResizeColumnIndex(point, out var resizeColumnIndex))
        {
            _resizingColumnIndex = resizeColumnIndex;
            _resizeAnchorX = point.X;
            _resizeOriginalWidth = _columnLayout.Widths[resizeColumnIndex];
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        var hit = _tableController.HitTest(point.X, point.Y);
        if (hit.Region == GridRegionKind.CornerHeader)
        {
            _tableController.Selection.SelectAll(Rows.Count, Headers.Count);
            Focus();
            RaiseSelectionChanged();
            InvalidateVisual();
            return;
        }

        _tableController.HandlePointerSelection(hit, e.KeyModifiers);
        if (hit.Region is GridRegionKind.Cell or GridRegionKind.RowHeader or GridRegionKind.ColumnHeader)
        {
            _isSelecting = true;
            _selectionOriginRegion = hit.Region;
            e.Pointer.Capture(this);
        }

        RaiseFocusedCellChanged();
        RaiseSelectionChanged();

        Focus();
        InvalidateVisual();
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (!CanEditCells || Headers.Count == 0 || Rows.Count == 0)
            return;

        var point = e.GetPosition(this);
        _tableController.SetDimensions(Rows.Count, Headers.Count);
        UpdateControllerViewport();
        var hit = _tableController.HitTest(point.X, point.Y);
        if (hit.Region == GridRegionKind.Cell && hit.RowIndex >= 0 && hit.ColumnIndex >= 0)
        {
            BeginEdit(new GridCellAddress(hit.RowIndex, hit.ColumnIndex));
            e.Handled = true;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(this);
        if (_resizingColumnIndex is not null)
        {
            Cursor = new Cursor(StandardCursorType.SizeWestEast);
            var width = _resizeOriginalWidth + (point.X - _resizeAnchorX);
            if (_columnLayout.SetWidth(_resizingColumnIndex.Value, width))
            {
                _rowRenderCache.InvalidateColumn(_resizingColumnIndex.Value);
                RequestRefresh(scroll: true, measure: true, visual: true);
            }

            e.Handled = true;
            return;
        }

        Cursor = TryGetResizeColumnIndex(point, out _)
            ? new Cursor(StandardCursorType.SizeWestEast)
            : new Cursor(StandardCursorType.Arrow);

        if (!_isSelecting)
            return;

        _tableController.SetDimensions(Rows.Count, Headers.Count);
        UpdateControllerViewport();
        var hit = _tableController.HitTest(point.X, point.Y);

        switch (_selectionOriginRegion)
        {
            case GridRegionKind.Cell when hit.Region == GridRegionKind.Cell && hit.RowIndex >= 0 && hit.ColumnIndex >= 0:
                _tableController.Selection.ExtendToCell(new GridCellAddress(hit.RowIndex, hit.ColumnIndex));
                break;
            case GridRegionKind.RowHeader when hit.RowIndex >= 0:
                _tableController.Selection.ExtendToRow(hit.RowIndex, Headers.Count);
                break;
            case GridRegionKind.ColumnHeader when hit.ColumnIndex >= 0:
                _tableController.Selection.ExtendToColumn(hit.ColumnIndex, Rows.Count);
                break;
            default:
                return;
        }

        InvalidateVisual();
        RaiseSelectionChanged();
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_resizingColumnIndex is not null)
        {
            _resizingColumnIndex = null;
            Cursor = new Cursor(StandardCursorType.Arrow);
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (_isSelecting)
        {
            _isSelecting = false;
            _selectionOriginRegion = GridRegionKind.None;
            e.Pointer.Capture(null);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (_resizingColumnIndex is null)
            Cursor = new Cursor(StandardCursorType.Arrow);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (Headers.Count == 0 || Rows.Count == 0)
            return;

        var direction = e.Key switch
        {
            Key.Up => GridNavigationDirection.Up,
            Key.Down => GridNavigationDirection.Down,
            Key.Left => GridNavigationDirection.Left,
            Key.Right => GridNavigationDirection.Right,
            Key.PageUp => GridNavigationDirection.PageUp,
            Key.PageDown => GridNavigationDirection.PageDown,
            Key.Home => GridNavigationDirection.Home,
            Key.End => GridNavigationDirection.End,
            _ => (GridNavigationDirection?)null
        };

        if (e.Key == Key.C &&
            (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            CopySelectionToClipboard();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter or Key.F2)
        {
            BeginEditAtFocusedCell();
            e.Handled = true;
            return;
        }

        if (direction is null)
            return;

        _tableController.SetDimensions(Rows.Count, Headers.Count);
        UpdateControllerViewport();
        var step = direction is GridNavigationDirection.PageUp or GridNavigationDirection.PageDown
            ? Math.Max(1, _tableController.VisibleRowCount)
            : 1;
        var result = e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            ? _tableController.ExtendFocus(direction.Value, step)
            : _tableController.MoveFocus(direction.Value, step);
        Offset = ToScrollViewerOffset(result.HorizontalOffset, result.VerticalOffset);
        RaiseFocusedCellChanged();
        RaiseSelectionChanged();
        InvalidateVisual();
        e.Handled = true;
    }

    public void BeginEditAtFocusedCell()
    {
        var focusedCell = _tableController.Selection.FocusCell;
        if (focusedCell is null)
            return;

        BeginEdit(focusedCell.Value);
    }

    public void CommitEdit(object? input)
    {
        if (_editorHost.CurrentSession is null)
            return;

        _editorHost.ApplyInput(input);
        var result = _editorHost.Commit();
        AdvanceFocusAfterEdit(result.Cell);
        CellEditCommitted?.Invoke(this, new GridCellEditCommittedEventArgs(result));
        InvalidateVisual();
    }

    public void CancelEdit()
    {
        if (_editorHost.CurrentSession is null)
            return;

        _editorHost.Cancel();
        CellEditCanceled?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    private async void CopySelectionToClipboard()
    {
        if (_tableController.Selection.Ranges.Count == 0)
            return;

        var text = _clipboardBuilder.Build(_tableController.Selection.Ranges, Rows, ColumnTypes);
        if (string.IsNullOrEmpty(text))
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
            return;

        await topLevel.Clipboard.SetTextAsync(text);
    }

    private void BeginEdit(GridCellAddress cell)
    {
        if (!CanEditCells || !CanEditCell(cell))
            return;

        var row = Rows[cell.Row];
        var value = row[cell.Column];
        var session = _editorHost.BeginEdit(cell, ColumnTypes.ElementAtOrDefault(cell.Column), value);
        var bounds = _tableController.GetCellBounds(cell.Row, cell.Column);
        CellEditRequested?.Invoke(this, new GridCellEditRequestedEventArgs(session, bounds));
    }

    private bool CanEditCell(GridCellAddress cell)
    {
        if (cell.Row < 0 || cell.Row >= Rows.Count || cell.Column < 0 || cell.Column >= Headers.Count)
            return false;

        if (!ReadOnlyColumns.Contains(cell.Column))
            return true;

        return Rows[cell.Row] is IGridEditableRow editableRow &&
               editableRow.VisualState == GridEditableRowVisualState.New;
    }

    private void RaiseFocusedCellChanged()
    {
        FocusedCellChanged?.Invoke(this, new GridFocusedCellChangedEventArgs(_tableController.Selection.FocusCell));
    }

    private void RaiseSelectionChanged()
    {
        SelectionChanged?.Invoke(
            this,
            new GridSelectionChangedEventArgs(_tableController.Selection.FocusCell, _tableController.Selection.Ranges.ToArray()));
    }

    private void AdvanceFocusAfterEdit(GridCellAddress cell)
    {
        if (cell.Column + 1 >= Headers.Count)
        {
            RaiseFocusedCellChanged();
            RaiseSelectionChanged();
            return;
        }

        var nextCell = new GridCellAddress(cell.Row, cell.Column + 1);
        var visible = _tableController.EnsureVisible(nextCell, GridNavigationDirection.Right);
        _tableController.Selection.SelectCell(visible.Cell);
        Offset = ToScrollViewerOffset(visible.HorizontalOffset, visible.VerticalOffset);
        RaiseFocusedCellChanged();
        RaiseSelectionChanged();
    }

    private void DrawHeaders(DrawingContext context, GridVisibleRange range)
    {
        var state = _tableController.State;
        var rowHeaderWidth = state.Viewport.RowHeaderWidth;
        var corner = _tableController.GetCornerHeaderBounds();
        context.DrawRectangle(HeaderBackground, GetBorderPen(), new Rect(corner.X, corner.Y, corner.Width, corner.Height));

        var clip = new Rect(rowHeaderWidth, 0, Math.Max(0, Bounds.Width - rowHeaderWidth), HeaderHeight);
        using (context.PushClip(clip))
        {
            for (var columnIndex = range.Start; columnIndex < range.EndExclusive; columnIndex++)
            {
                var bounds = _tableController.GetColumnHeaderBounds(columnIndex);
                var rect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                context.DrawRectangle(HeaderBackground, GetBorderPen(), rect);
                DrawText(context, Headers[columnIndex], new Rect(bounds.X + 6, bounds.Y, Math.Max(0, bounds.Width - 12), bounds.Height), GridColumnAlignment.Left, HeaderForeground);
            }
        }
    }

    private void DrawRowHeaders(DrawingContext context, GridVisibleRange range)
    {
        var state = _tableController.State;
        var rowHeaderWidth = state.Viewport.RowHeaderWidth;
        var clip = new Rect(0, HeaderHeight, rowHeaderWidth, Math.Max(0, Bounds.Height - HeaderHeight));
        using (context.PushClip(clip))
        {
            for (var rowIndex = range.Start; rowIndex < range.EndExclusive; rowIndex++)
            {
                var bounds = _tableController.GetRowHeaderBounds(rowIndex);
                var rect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                var brush = _tableController.Selection.Contains(new GridCellAddress(rowIndex, 0))
                    ? SelectionBackground
                    : GetRowBackgroundBrush(rowIndex) ?? HeaderBackground;
                context.DrawRectangle(brush, GetBorderPen(), rect);
                DrawText(context, (rowIndex + 1).ToString(CultureInfo.InvariantCulture), new Rect(bounds.X + 6, bounds.Y, Math.Max(0, bounds.Width - 12), bounds.Height), GridColumnAlignment.Left, HeaderForeground);
            }
        }
    }

    private void DrawCells(DrawingContext context, GridViewportInfo viewportInfo)
    {
        var rowHeaderWidth = _tableController.State.Viewport.RowHeaderWidth;
        var verticalLines = new List<double>();
        var horizontalLines = new List<double>();
        double? maxVisibleRight = null;
        double? maxVisibleBottom = null;
        var clip = new Rect(rowHeaderWidth, HeaderHeight, Math.Max(0, Bounds.Width - rowHeaderWidth), Math.Max(0, Bounds.Height - HeaderHeight));
        using (context.PushClip(clip))
        {
            for (var rowIndex = viewportInfo.Rows.Start; rowIndex < viewportInfo.Rows.EndExclusive; rowIndex++)
            {
                var row = Rows[rowIndex];
                var hasCachedRow = _rowRenderCache.TryGetRow(rowIndex, out var cachedRow);
                GridCellBounds? firstBounds = null;
                GridCellBounds? lastBounds = null;

                for (var columnIndex = viewportInfo.Columns.Start; columnIndex < viewportInfo.Columns.EndExclusive; columnIndex++)
                {
                    var bounds = _tableController.GetCellBounds(rowIndex, columnIndex);
                    firstBounds ??= bounds;
                    lastBounds = bounds;
                    maxVisibleRight = Math.Max(maxVisibleRight ?? double.MinValue, bounds.X + bounds.Width);
                    maxVisibleBottom = Math.Max(maxVisibleBottom ?? double.MinValue, bounds.Y + bounds.Height);
                    var rect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                    var brush = _tableController.Selection.Contains(new GridCellAddress(rowIndex, columnIndex))
                        ? SelectionBackground
                        : GetRowBackgroundBrush(rowIndex) ?? CellBackground;
                    context.DrawRectangle(brush, null, rect);
                    if (row is IGridEditableRow editableRow && editableRow.InvalidColumnIndexes.Contains(columnIndex))
                        context.DrawRectangle(null, ValidationErrorPen, rect.Deflate(1));

                    if (rowIndex == viewportInfo.Rows.Start)
                        verticalLines.Add(bounds.X);
                    if (columnIndex == viewportInfo.Columns.EndExclusive - 1)
                        verticalLines.Add(bounds.X + bounds.Width);

                    if (hasCachedRow && cachedRow is not null && columnIndex < cachedRow.Length && cachedRow[columnIndex] is not null)
                    {
                        DrawFormattedText(
                            context,
                            cachedRow[columnIndex].FormattedText,
                            new Rect(bounds.X + 6, bounds.Y, Math.Max(0, bounds.Width - 12), bounds.Height),
                            cachedRow[columnIndex].Alignment);
                        continue;
                    }

                    var value = columnIndex < row.Count ? row[columnIndex] : null;
                    var renderer = GetColumnRenderer(columnIndex, value);
                    var text = renderer.FormatValue(value, GridRendererContext.Default);
                    DrawText(context, text, new Rect(bounds.X + 6, bounds.Y, Math.Max(0, bounds.Width - 12), bounds.Height), renderer.Alignment, CellForeground);
                }

                if (firstBounds.HasValue && lastBounds.HasValue)
                {
                    horizontalLines.Add(firstBounds.Value.Y);
                    horizontalLines.Add(lastBounds.Value.Y + lastBounds.Value.Height);
                }
            }

            var gridTop = HeaderHeight;
            var gridBottom = Math.Max(gridTop, maxVisibleBottom ?? gridTop);
            var borderPen = GetBorderPen();
            foreach (var x in verticalLines.Distinct().OrderBy(static v => v))
            {
                context.DrawLine(borderPen, new Point(x, gridTop), new Point(x, gridBottom));
            }

            var gridLeft = rowHeaderWidth;
            var gridRight = Math.Max(gridLeft, maxVisibleRight ?? gridLeft);
            foreach (var y in horizontalLines.Distinct().OrderBy(static v => v))
            {
                context.DrawLine(borderPen, new Point(gridLeft, y), new Point(gridRight, y));
            }
        }
    }

    private IBrush? GetRowBackgroundBrush(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= Rows.Count || Rows[rowIndex] is not IGridEditableRow editableRow)
            return null;

        return editableRow.VisualState switch
        {
            GridEditableRowVisualState.New => new SolidColorBrush(Color.Parse("#1A6900cc")),
            GridEditableRowVisualState.Modified => new SolidColorBrush(Color.Parse("#1A0066CC")),
            _ => null
        };
    }

    private void DrawText(DrawingContext context, string text, Rect rect, GridColumnAlignment alignment, IBrush foregroundBrush)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, GetTypeface(), GridFontSize, foregroundBrush);
        DrawFormattedText(context, formatted, rect, alignment);
    }

    private static void DrawFormattedText(DrawingContext context, FormattedText formatted, Rect rect, GridColumnAlignment alignment)
    {
        var x = alignment switch
        {
            GridColumnAlignment.Right => rect.Right - formatted.Width - 6,
            GridColumnAlignment.Center => rect.X + Math.Max(0, (rect.Width - formatted.Width) / 2),
            _ => rect.X
        };

        var y = rect.Y + Math.Max(0, (rect.Height - formatted.Height) / 2);
        context.DrawText(formatted, new Point(x, y));
    }

    private void InitializeColumnWidthsIfNeeded()
    {
        if (!_autoWidthPending || Headers.Count == 0)
            return;

        if (Rows.Count == 0)
            return;

        for (var columnIndex = 0; columnIndex < Headers.Count; columnIndex++)
        {
            _columnLayout.TrackWidth(columnIndex, MeasureTextWidth(Headers[columnIndex], HeaderForeground) + 16);

            for (var rowIndex = 0; rowIndex < Rows.Count; rowIndex++)
            {
                var row = Rows[rowIndex];
                var value = columnIndex < row.Count ? row[columnIndex] : null;
                var renderer = GetColumnRenderer(columnIndex, value);
                var width = renderer.MeasureWidth(value, GridRendererContext.Default, text => MeasureTextWidth(text, CellForeground)) + 16;
                _columnLayout.TrackWidth(columnIndex, width);
            }
        }

        _lastMeasuredRowHeaderWidth = GetRowHeaderWidth();
        _autoWidthPending = false;
    }

    private double MeasureTextWidth(string text, IBrush foregroundBrush)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, GetTypeface(), GridFontSize, foregroundBrush);
        return formatted.Width;
    }

    private double GetRowHeaderWidth()
    {
        var countText = Math.Max(1, Rows.Count).ToString(CultureInfo.InvariantCulture);
        return Math.Max(44, MeasureTextWidth(countText, HeaderForeground) + 16);
    }

    private Typeface GetTypeface() => new(GridFontFamily);

    private Pen GetBorderPen() => new(GridLine, 1);

    private static void ReplaceSubscription<T>(
        ref ObservableCollection<T>? current,
        ObservableCollection<T> next,
        NotifyCollectionChangedEventHandler handler)
    {
        if (current is not null)
            current.CollectionChanged -= handler;

        current = next;
        current.CollectionChanged += handler;
    }

    private void RequestRefresh(bool scroll = false, bool measure = false, bool visual = false)
    {
        _pendingScrollInvalidation |= scroll;
        _pendingMeasureInvalidation |= measure;
        _pendingVisualInvalidation |= visual;

        if (_isRefreshScheduled)
            return;

        _isRefreshScheduled = true;
        Dispatcher.UIThread.Post(FlushRefreshRequests, DispatcherPriority.Render);
    }

    private void FlushRefreshRequests()
    {
        _isRefreshScheduled = false;

        if (_pendingScrollInvalidation)
            RaiseScrollInvalidated(EventArgs.Empty);

        if (_pendingMeasureInvalidation)
            InvalidateMeasure();

        if (_pendingVisualInvalidation)
            InvalidateVisual();

        _pendingScrollInvalidation = false;
        _pendingMeasureInvalidation = false;
        _pendingVisualInvalidation = false;
    }

    private void UpdateControllerViewport()
    {
        _tableController.UpdateViewport(new GridViewportState(
            GetContentOffsetX(),
            GetContentOffsetY(),
            Viewport.Width,
            Viewport.Height,
            GetRowHeaderWidth(),
            HeaderHeight,
            RowHeight));
    }

    private double GetContentOffsetX()
    {
        return Math.Max(0, Offset.X - GetRowHeaderWidth());
    }

    private double GetContentOffsetY()
    {
        return Math.Max(0, Offset.Y - HeaderHeight);
    }

    private Vector ToScrollViewerOffset(double contentOffsetX, double contentOffsetY)
    {
        return new Vector(
            Math.Max(0, contentOffsetX + GetRowHeaderWidth()),
            Math.Max(0, contentOffsetY + HeaderHeight));
    }

    private Vector ClampOffset(Vector value)
    {
        var maxX = CanHorizontallyScroll ? Math.Max(0, Extent.Width - Viewport.Width) : 0;
        var maxY = CanVerticallyScroll ? Math.Max(0, Extent.Height - Viewport.Height) : 0;
        return new Vector(
            Math.Clamp(value.X, 0, maxX),
            Math.Clamp(value.Y, 0, maxY));
    }

    private bool TryGetResizeColumnIndex(Point point, out int columnIndex)
    {
        columnIndex = -1;

        if (point.Y < 0 || point.Y > HeaderHeight)
            return false;

        UpdateControllerViewport();
        var columns = _tableController.GetColumnRenderRange();
        for (var index = columns.Start; index < columns.EndExclusive; index++)
        {
            var bounds = _tableController.GetColumnHeaderBounds(index);
            if (Math.Abs(point.X - (bounds.X + bounds.Width)) <= ResizeHandleHalfWidth)
            {
                columnIndex = index;
                return true;
            }
        }

        return false;
    }

    private IGridCellRenderer GetColumnRenderer(int columnIndex, object? value)
    {
        while (_columnRenderers.Count <= columnIndex)
            _columnRenderers.Add(null);

        var cached = _columnRenderers[columnIndex];
        if (cached is not null)
            return cached;

        var valueType = columnIndex < ColumnTypes.Count ? ColumnTypes[columnIndex] : value?.GetType();
        var renderer = _rendererRegistry.Resolve(valueType, value);
        _columnRenderers[columnIndex] = renderer;
        return renderer;
    }

    internal GridCellAddress? GetFocusedCellForTest()
    {
        _tableController.SetDimensions(Rows.Count, Headers.Count);
        UpdateControllerViewport();
        return _tableController.Selection.FocusCell;
    }

    internal int GetVisibleRowCountForTest()
    {
        _tableController.SetDimensions(Rows.Count, Headers.Count);
        UpdateControllerViewport();
        return _tableController.VisibleRowCount;
    }

    internal GridCellBounds GetCellBoundsForTest(int rowIndex, int columnIndex)
    {
        _tableController.SetDimensions(Rows.Count, Headers.Count);
        UpdateControllerViewport();
        return _tableController.GetCellBounds(rowIndex, columnIndex);
    }

    internal Point GetCellClickPointForTest(int rowIndex, int columnIndex)
    {
        var bounds = GetCellBoundsForTest(rowIndex, columnIndex);
        var localPoint = new Point(bounds.X + Math.Min(12, bounds.Width / 2), bounds.Y + Math.Min(12, bounds.Height / 2));
        var root = this.GetVisualRoot();
        return root is Visual visualRoot
            ? Avalonia.VisualExtensions.TranslatePoint(this, localPoint, visualRoot) ?? localPoint
            : localPoint;
    }

    internal void SelectCellAtLocalPointForTest(Point point)
    {
        _tableController.SetDimensions(Rows.Count, Headers.Count);
        UpdateControllerViewport();
        var hit = _tableController.HitTest(point.X, point.Y);
        _tableController.HandlePointerSelection(hit, KeyModifiers.None);
        RaiseSelectionChanged();
    }

    internal GridHitTestResult HitTestAtLocalPointForTest(Point point)
    {
        _tableController.SetDimensions(Rows.Count, Headers.Count);
        UpdateControllerViewport();
        return _tableController.HitTest(point.X, point.Y);
    }

    internal void DragSelectCellsForTest(GridCellAddress start, GridCellAddress end)
    {
        _tableController.SetDimensions(Rows.Count, Headers.Count);
        UpdateControllerViewport();
        _tableController.Selection.SelectCell(start);
        _tableController.Selection.ExtendToCell(end);
        RaiseSelectionChanged();
    }

    internal bool SelectionContainsForTest(GridCellAddress cell)
    {
        _tableController.SetDimensions(Rows.Count, Headers.Count);
        UpdateControllerViewport();
        return _tableController.Selection.Contains(cell);
    }

    internal void SetScrollBarReserve(double horizontalReserve, double verticalReserve)
    {
        horizontalReserve = Math.Max(0, horizontalReserve);
        verticalReserve = Math.Max(0, verticalReserve);

        if (Math.Abs(_horizontalScrollBarReserve - horizontalReserve) < 0.01d &&
            Math.Abs(_verticalScrollBarReserve - verticalReserve) < 0.01d)
            return;

        _horizontalScrollBarReserve = horizontalReserve;
        _verticalScrollBarReserve = verticalReserve;
        Offset = ClampOffset(Offset);
        RaiseScrollInvalidated(EventArgs.Empty);
        InvalidateMeasure();
        InvalidateVisual();
    }
}
