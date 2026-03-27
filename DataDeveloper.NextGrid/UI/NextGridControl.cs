using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
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
