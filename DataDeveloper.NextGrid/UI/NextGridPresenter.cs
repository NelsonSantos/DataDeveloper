using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using DataDeveloper.NextGrid.Renderers;

namespace DataDeveloper.NextGrid.UI;

internal sealed class NextGridPresenter : Control, IScrollable, ILogicalScrollable
{
    private const double DefaultColumnWidth = 120;
    private const double HeaderHeight = 34;
    private const double RowHeight = 28;
    private const double ResizeHandleHalfWidth = 4;

    private readonly GridColumnLayoutEngine _columnLayout = new(DefaultColumnWidth);
    private readonly GridTableController _tableController;
    private readonly GridRendererRegistry _rendererRegistry = new();
    private readonly Typeface _typeface = new("Consolas");
    private readonly Pen _borderPen = new(Brushes.Gray, 1);
    private readonly IBrush _headerBrush = Brushes.LightGray;
    private readonly IBrush _backgroundBrush = Brushes.White;
    private readonly IBrush _textBrush = Brushes.Black;
    private readonly IBrush _selectionBrush = Brushes.LightBlue;
    private Vector _offset;
    private bool _autoWidthPending = true;
    private readonly List<IGridCellRenderer?> _columnRenderers = [];
    private int? _resizingColumnIndex;
    private double _resizeAnchorX;
    private double _resizeOriginalWidth;
    private double _lastMeasuredRowHeaderWidth = 44;

    private ObservableCollection<string>? _headersSubscription;
    private ObservableCollection<Type>? _typesSubscription;
    private ObservableCollection<IReadOnlyList<object?>>? _rowsSubscription;

    public static readonly StyledProperty<ObservableCollection<string>> HeadersProperty =
        AvaloniaProperty.Register<NextGridPresenter, ObservableCollection<string>>(nameof(Headers), defaultValue: []);

    public static readonly StyledProperty<ObservableCollection<Type>> ColumnTypesProperty =
        AvaloniaProperty.Register<NextGridPresenter, ObservableCollection<Type>>(nameof(ColumnTypes), defaultValue: []);

    public static readonly StyledProperty<ObservableCollection<IReadOnlyList<object?>>> RowsProperty =
        AvaloniaProperty.Register<NextGridPresenter, ObservableCollection<IReadOnlyList<object?>>>(nameof(Rows), defaultValue: []);

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

    public Size Extent => new(
        GetRowHeaderWidth() + _columnLayout.GetTotalWidth(),
        HeaderHeight + (Rows.Count * RowHeight));

    public Vector Offset
    {
        get => _offset;
        set
        {
            var clamped = ClampOffset(value);
            if (_offset == clamped)
                return;

            _offset = clamped;
            RaiseScrollInvalidated(EventArgs.Empty);
            InvalidateVisual();
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

    public NextGridPresenter()
    {
        var layoutEngine = new GridLayoutEngine(_columnLayout);
        var viewportEngine = new GridViewportEngine(_columnLayout);
        var selection = new GridSelectionModel();
        _tableController = new GridTableController(_columnLayout, layoutEngine, viewportEngine, selection);
        Focusable = true;
        PropertyChanged += OnControlPropertyChanged;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
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

        var nextOffset = Offset;
        var contentViewportWidth = Math.Max(0, Viewport.Width - GetRowHeaderWidth());
        var contentViewportHeight = Math.Max(0, Viewport.Height - HeaderHeight);

        if (targetRect.X < nextOffset.X)
            nextOffset = new Vector(targetRect.X, nextOffset.Y);
        else if (targetRect.Right > nextOffset.X + contentViewportWidth)
            nextOffset = new Vector(targetRect.Right - contentViewportWidth, nextOffset.Y);

        if (targetRect.Y < nextOffset.Y)
            nextOffset = new Vector(nextOffset.X, targetRect.Y);
        else if (targetRect.Bottom > nextOffset.Y + contentViewportHeight)
            nextOffset = new Vector(nextOffset.X, targetRect.Bottom - contentViewportHeight);

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
        }

        if (e.Property == ColumnTypesProperty)
        {
            ReplaceSubscription(ref _typesSubscription, ColumnTypes, OnCollectionChanged);
            _autoWidthPending = true;
            _columnRenderers.Clear();
        }

        if (e.Property == RowsProperty)
        {
            ReplaceSubscription(ref _rowsSubscription, Rows, OnCollectionChanged);
            _autoWidthPending = true;
            _columnRenderers.Clear();
        }

        if (e.Property == BoundsProperty)
        {
            Offset = ClampOffset(Offset);
            RaiseScrollInvalidated(EventArgs.Empty);
            InvalidateVisual();
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _autoWidthPending = true;
            _columnRenderers.Clear();
        }

        var rowHeaderWidthChanged = Math.Abs(GetRowHeaderWidth() - _lastMeasuredRowHeaderWidth) > 0.01d;
        Offset = ClampOffset(Offset);
        RaiseScrollInvalidated(EventArgs.Empty);

        if (e.Action == NotifyCollectionChangedAction.Reset || rowHeaderWidthChanged)
            InvalidateMeasure();

        InvalidateVisual();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Headers.Count == 0 || Rows.Count == 0)
            return;

        Focus();
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
        _tableController.HandlePointerSelection(hit, e.KeyModifiers);
        InvalidateVisual();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_resizingColumnIndex is null)
            return;

        var point = e.GetPosition(this);
        var width = _resizeOriginalWidth + (point.X - _resizeAnchorX);
        if (_columnLayout.SetWidth(_resizingColumnIndex.Value, width))
        {
            RaiseScrollInvalidated(EventArgs.Empty);
            InvalidateMeasure();
            InvalidateVisual();
        }

        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_resizingColumnIndex is null)
            return;

        _resizingColumnIndex = null;
        e.Pointer.Capture(null);
        e.Handled = true;
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
            _ => (GridNavigationDirection?)null
        };

        if (direction is null)
            return;

        _tableController.SetDimensions(Rows.Count, Headers.Count);
        UpdateControllerViewport();
        var result = _tableController.MoveFocus(direction.Value);
        Offset = new Vector(result.HorizontalOffset, result.VerticalOffset);
        InvalidateVisual();
        e.Handled = true;
    }

    private void DrawHeaders(DrawingContext context, GridVisibleRange range)
    {
        var state = _tableController.State;
        var rowHeaderWidth = state.Viewport.RowHeaderWidth;
        var corner = _tableController.GetCornerHeaderBounds();
        context.DrawRectangle(_headerBrush, _borderPen, new Rect(corner.X, corner.Y, corner.Width, corner.Height));

        var clip = new Rect(rowHeaderWidth, 0, Math.Max(0, Bounds.Width - rowHeaderWidth), HeaderHeight);
        using (context.PushClip(clip))
        {
            for (var columnIndex = range.Start; columnIndex < range.EndExclusive; columnIndex++)
            {
                var bounds = _tableController.GetColumnHeaderBounds(columnIndex);
                var rect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                context.DrawRectangle(_headerBrush, _borderPen, rect);
                DrawText(context, Headers[columnIndex], new Rect(bounds.X + 6, bounds.Y, Math.Max(0, bounds.Width - 12), bounds.Height), GridColumnAlignment.Left);
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
                var brush = _tableController.Selection.Contains(new GridCellAddress(rowIndex, 0)) ? _selectionBrush : _headerBrush;
                context.DrawRectangle(brush, _borderPen, rect);
                DrawText(context, (rowIndex + 1).ToString(CultureInfo.InvariantCulture), new Rect(bounds.X + 6, bounds.Y, Math.Max(0, bounds.Width - 12), bounds.Height), GridColumnAlignment.Left);
            }
        }
    }

    private void DrawCells(DrawingContext context, GridViewportInfo viewportInfo)
    {
        var rowHeaderWidth = _tableController.State.Viewport.RowHeaderWidth;
        var clip = new Rect(rowHeaderWidth, HeaderHeight, Math.Max(0, Bounds.Width - rowHeaderWidth), Math.Max(0, Bounds.Height - HeaderHeight));
        using (context.PushClip(clip))
        {
            for (var rowIndex = viewportInfo.Rows.Start; rowIndex < viewportInfo.Rows.EndExclusive; rowIndex++)
            {
                var row = Rows[rowIndex];

                for (var columnIndex = viewportInfo.Columns.Start; columnIndex < viewportInfo.Columns.EndExclusive; columnIndex++)
                {
                    var bounds = _tableController.GetCellBounds(rowIndex, columnIndex);
                    var rect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                    var brush = _tableController.Selection.Contains(new GridCellAddress(rowIndex, columnIndex)) ? _selectionBrush : _backgroundBrush;
                    context.DrawRectangle(brush, _borderPen, rect);

                    var value = columnIndex < row.Count ? row[columnIndex] : null;
                    var renderer = GetColumnRenderer(columnIndex, value);
                    var text = renderer.FormatValue(value, GridRendererContext.Default);
                    DrawText(context, text, new Rect(bounds.X + 6, bounds.Y, Math.Max(0, bounds.Width - 12), bounds.Height), renderer.Alignment);
                }
            }
        }
    }

    private void DrawText(DrawingContext context, string text, Rect rect, GridColumnAlignment alignment)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _typeface, 12, _textBrush);
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
            _columnLayout.TrackWidth(columnIndex, MeasureTextWidth(Headers[columnIndex]) + 16);

            for (var rowIndex = 0; rowIndex < Rows.Count; rowIndex++)
            {
                var row = Rows[rowIndex];
                var value = columnIndex < row.Count ? row[columnIndex] : null;
                var renderer = GetColumnRenderer(columnIndex, value);
                var width = renderer.MeasureWidth(value, GridRendererContext.Default, MeasureTextWidth) + 16;
                _columnLayout.TrackWidth(columnIndex, width);
            }
        }

        _lastMeasuredRowHeaderWidth = GetRowHeaderWidth();
        _autoWidthPending = false;
    }

    private double MeasureTextWidth(string text)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _typeface, 12, _textBrush);
        return formatted.Width;
    }

    private double GetRowHeaderWidth()
    {
        var countText = Math.Max(1, Rows.Count).ToString(CultureInfo.InvariantCulture);
        return Math.Max(44, MeasureTextWidth(countText) + 16);
    }

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

    private void UpdateControllerViewport()
    {
        _tableController.UpdateViewport(new GridViewportState(
            Offset.X,
            Offset.Y,
            Viewport.Width,
            Viewport.Height,
            GetRowHeaderWidth(),
            HeaderHeight,
            RowHeight));
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
    }
}
