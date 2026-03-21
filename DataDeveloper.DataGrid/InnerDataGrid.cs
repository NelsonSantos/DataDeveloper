// InnerDataGrid: rendering, multiple selection, scroll sync, keyboard navigation

using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace DataDeveloper.DataGrid
{
    internal class InnerDataGrid : Control
    {
        public static readonly StyledProperty<ObservableCollection<string>> HeadersProperty =
            AvaloniaProperty.Register<InnerDataGrid, ObservableCollection<string>>(nameof(Headers));

        public static readonly StyledProperty<ObservableCollection<IEnumerable<object>>> DataProperty =
            AvaloniaProperty.Register<InnerDataGrid, ObservableCollection<IEnumerable<object>>>(nameof(Data));

        public ObservableCollection<string> Headers
        {
            get => GetValue(HeadersProperty);
            set => SetValue(HeadersProperty, value);
        }

        public ObservableCollection<IEnumerable<object>> Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        private HashSet<(int Row, int Col)> _selectedCells = new();
        private (int Row, int Col)? _anchorCell;

        private const int _defaultCellWidth = 120;
        private const int _cellHeight = 30;
        private const int _headerHeight = 40;
      
        private readonly Typeface _typeface = new Typeface("Arial");
      
        private readonly Pen _borderPen = new Pen(Brushes.Gray, 1);
        private readonly IBrush _headerBrush = Brushes.LightGray;
        private readonly IBrush _backgroundBrush = Brushes.White;
        private readonly IBrush _textBrush = Brushes.Black;
        private readonly IBrush _selectionBrush = Brushes.LightBlue;        
      
        private int RowHeaderWidth => Math.Max(MeasureTextWidth((Data.Count).ToString(), 14), 40) + 10;

        public InnerDataGrid()
        {
            Focusable = true;
            KeyDown += OnKeyDown;
            PointerPressed += OnPointerPressed;

            this.PropertyChanged += (_, e) =>
            {
                if (e.Property == HeadersProperty)
                    Headers.CollectionChanged += (_, _) => InvalidateMeasure();
                else if (e.Property == DataProperty)
                    Data.CollectionChanged += (_, _) => InvalidateMeasure();
            };
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetPosition(this);
            var presenter = this.GetVisualAncestors().OfType<ScrollContentPresenter>().FirstOrDefault();
            var offset = presenter?.Offset ?? default;

            int row = (int)((point.Y + offset.Y - _headerHeight) / _cellHeight);

            if (row >= 0 && row < Data.Count && point.X <= RowHeaderWidth)
            {
                _selectedCells.Clear();
                for (int col = 0; col < Headers.Count; col++)
                    _selectedCells.Add((row, col));
                _anchorCell = (row, 0);
            }
            else if (row >= 0 && row < Data.Count)
            {
                int col = (int)((point.X - RowHeaderWidth) / _defaultCellWidth);
                if (col >= 0 && col < Headers.Count)
                {
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    {
                        if (!_selectedCells.Add((row, col)))
                            _selectedCells.Remove((row, col));
                    }
                    else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _anchorCell.HasValue)
                    {
                        _selectedCells.Clear();
                        var (anchorRow, anchorCol) = _anchorCell.Value;
                        for (int r = Math.Min(anchorRow, row); r <= Math.Max(anchorRow, row); r++)
                            for (int c = Math.Min(anchorCol, col); c <= Math.Max(anchorCol, col); c++)
                                _selectedCells.Add((r, c));
                    }
                    else
                    {
                        _selectedCells.Clear();
                        _selectedCells.Add((row, col));
                        _anchorCell = (row, col);
                    }
                }
            }

            InvalidateVisual();
            this.Focus();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.A)
            {
                _selectedCells.Clear();
                for (int r = 0; r < Data.Count; r++)
                    for (int c = 0; c < Headers.Count; c++)
                        _selectedCells.Add((r, c));
                _anchorCell = null;
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Escape)
            {
                _selectedCells.Clear();
                _anchorCell = null;
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            else if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 && e.Key == Key.C)
            {
                var sorted = _selectedCells.OrderBy(s => s.Row).ThenBy(s => s.Col).ToList();
                var grouped = sorted.GroupBy(cell => cell.Row).OrderBy(g => g.Key);
                var lines = grouped.Select(g =>
                    string.Join("\t", g.OrderBy(c => c.Col).Select(c =>
                    {
                        var row = Data.ElementAtOrDefault(c.Row);
                        var rowArray = row is object[] arr ? arr : row?.ToArray();
                        return rowArray != null && c.Col < rowArray.Length ? rowArray[c.Col]?.ToString() : "";
                    }))).ToArray();
                var text = string.Join("\n", lines);
                TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(text);
                e.Handled = true;
                return;
            }

            if (_selectedCells.Count == 0 || Headers == null || Data == null)
                return;

            var current = _anchorCell ?? _selectedCells.Last();
            var (row, col) = current;
            int newRow = row;
            int newCol = col;

            switch (e.Key)
            {
                case Key.Up when row > 0:
                    newRow--;
                    break;
                case Key.Down when row < Data.Count - 1:
                    newRow++;
                    break;
                case Key.Left when col > 0:
                    newCol--;
                    break;
                case Key.Right when col < Headers.Count - 1:
                    newCol++;
                    break;
            }

            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _anchorCell.HasValue)
            {
                _selectedCells.Clear();
                var (anchorRow, anchorCol) = _anchorCell.Value;
                for (int r = Math.Min(anchorRow, newRow); r <= Math.Max(anchorRow, newRow); r++)
                    for (int c = Math.Min(anchorCol, newCol); c <= Math.Max(anchorCol, newCol); c++)
                        _selectedCells.Add((r, c));
            }
            else
            {
                _selectedCells.Clear();
                _selectedCells.Add((newRow, newCol));
                if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    _anchorCell = (newRow, newCol);
            }

            var scrollViewer = this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
            double cellTop = _headerHeight + newRow * _cellHeight;
            double cellBottom = cellTop + _cellHeight;
            double cellLeft = RowHeaderWidth + (newCol < 0 ? 0 : newCol * _defaultCellWidth);
            double cellRight = cellLeft + _defaultCellWidth;

            if (scrollViewer != null)
            {
                double topVisible = scrollViewer.Offset.Y;
                double bottomVisible = topVisible + scrollViewer.Viewport.Height;
                double leftVisible = scrollViewer.Offset.X;
                double rightVisible = leftVisible + scrollViewer.Viewport.Width;

                if (cellTop < topVisible)
                    scrollViewer.Offset = new Vector(scrollViewer.Offset.X, cellTop);
                else if (cellBottom > bottomVisible)
                    scrollViewer.Offset = new Vector(scrollViewer.Offset.X, cellBottom - scrollViewer.Viewport.Height);

                if (cellLeft < leftVisible)
                    scrollViewer.Offset = new Vector(cellLeft, scrollViewer.Offset.Y);
                else if (cellRight > rightVisible)
                    scrollViewer.Offset = new Vector(cellRight - scrollViewer.Viewport.Width, scrollViewer.Offset.Y);
            }

            InvalidateVisual();
            e.Handled = true;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (Headers == null || Data == null)
                return;

            var presenter = this.GetVisualAncestors().OfType<ScrollContentPresenter>().FirstOrDefault();
            var offset = presenter?.Offset ?? default;

            using (context.PushPostTransform(Matrix.CreateTranslation(-offset.X, -offset.Y)))
            {
                int columnCount = Headers.Count;
                int rowCount = Data.Count;
                double[] columnWidths = new double[columnCount];
                int startRow = Math.Max(0, (int)((offset.Y - _headerHeight) / _cellHeight));
                int visibleRowCount = (int)Math.Ceiling(Bounds.Height / _cellHeight) + 1;
                int endRow = Math.Min(rowCount, startRow + visibleRowCount);

                for (int col = 0; col < columnCount; col++)
                {
                    double maxWidth = MeasureTextWidth(Headers[col], 14);
                    for (int row = startRow; row < endRow; row++)
                    {
                        var value = Data[row]?.ElementAtOrDefault(col)?.ToString();
                        if (!string.IsNullOrEmpty(value))
                        {
                            double w = MeasureTextWidth(value, 14);
                            if (w > maxWidth)
                                maxWidth = w;
                        }
                    }
                    columnWidths[col] = Math.Max(_defaultCellWidth, maxWidth + 10);
                }

                double x = RowHeaderWidth;
                for (int col = 0; col < columnCount; col++)
                {
                    double width = columnWidths[col];
                    var rect = new Rect(x, 0, width, _headerHeight);
                    context.DrawRectangle(_headerBrush, _borderPen, rect);

                    var formattedText = new FormattedText(Headers[col], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _typeface, 14, _textBrush);
                    context.DrawText(formattedText, new Point(x + 5, 10));

                    x += width;
                }

                double yOffset = _headerHeight;
                for (int row = 0; row < rowCount; row++)
                {
                    double y = yOffset + row * _cellHeight;
                    var rect = new Rect(0, y, RowHeaderWidth, _cellHeight);
                    var headerCellBrush = _selectedCells.Any(c => c.Row == row) ? _selectionBrush : _headerBrush;
                    context.DrawRectangle(headerCellBrush, _borderPen, rect);

                    var formattedText = new FormattedText((row + 1).ToString(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _typeface, 14, _textBrush);
                    context.DrawText(formattedText, new Point(5, y + 8));
                }

                for (int row = startRow; row < endRow; row++)
                {
                    x = RowHeaderWidth;
                    var rowItems = Data[row];
                    var rowArray = rowItems as object[] ?? rowItems.ToArray();

                    for (int col = 0; col < columnCount; col++)
                    {
                        double width = columnWidths[col];
                        double y = _headerHeight + row * _cellHeight;
                        var rect = new Rect(x, y, width, _cellHeight);
                        var cellBrush = _selectedCells.Contains((row, col)) ? Brushes.LightBlue : _backgroundBrush;
                        context.DrawRectangle(cellBrush, _borderPen, rect);

                        if (col < rowArray.Length)
                        {
                            string value = rowArray[col]?.ToString() ?? "";
                            var formattedText = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _typeface, 14, _textBrush);
                            context.DrawText(formattedText, new Point(x + 5, y + 8));
                        }

                        x += width;
                    }
                }
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Headers == null || Data == null)
                return base.MeasureOverride(availableSize);

            var width = RowHeaderWidth + Headers.Count * _defaultCellWidth;
            var height = _headerHeight + Data.Count * _cellHeight;

            return new Size(width, height);
        }

        private int MeasureTextWidth(string text, double fontSize)
        {
            var typeface = new Typeface("Arial");
            var formattedText = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, fontSize, Brushes.Black);
            return (int)Math.Ceiling(formattedText.Width);
        }
    }
}
