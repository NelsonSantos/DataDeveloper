using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace DataDeveloper.NextGrid.UI;

public sealed class NextGridControl : UserControl
{
    private const double ScrollBarSafetyMargin = 20;

    private readonly ScrollViewer _scrollViewer;
    private readonly NextGridPresenter _presenter;

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
        ScrollViewer.SetBringIntoViewOnFocusChange(_presenter, false);

        Content = _scrollViewer;

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
        _presenter.AddHandler(Control.RequestBringIntoViewEvent, OnPresenterRequestBringIntoView, RoutingStrategies.Bubble);
        _scrollViewer.LayoutUpdated += OnScrollViewerLayoutUpdated;
    }

    internal GridCellAddress? GetFocusedCellForTest() => _presenter.GetFocusedCellForTest();

    internal int GetVisibleRowCountForTest() => _presenter.GetVisibleRowCountForTest();

    internal GridCellBounds GetCellBoundsForTest(int rowIndex, int columnIndex) =>
        _presenter.GetCellBoundsForTest(rowIndex, columnIndex);

    internal Point GetCellClickPointForTest(int rowIndex, int columnIndex) =>
        _presenter.GetCellClickPointForTest(rowIndex, columnIndex);

    internal void SelectCellAtLocalPointForTest(Point point) =>
        _presenter.SelectCellAtLocalPointForTest(point);

    internal GridHitTestResult HitTestAtLocalPointForTest(Point point) =>
        _presenter.HitTestAtLocalPointForTest(point);

    internal void DragSelectCellsForTest(GridCellAddress start, GridCellAddress end) =>
        _presenter.DragSelectCellsForTest(start, end);

    internal bool SelectionContainsForTest(GridCellAddress cell) =>
        _presenter.SelectionContainsForTest(cell);

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
}
