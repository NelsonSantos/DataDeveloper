using System.Collections.Concurrent;
using System.Globalization;
using Avalonia.Media;
using DataDeveloper.NextGrid.Renderers;

namespace DataDeveloper.NextGrid.UI;

internal sealed class RowRenderCache
{
    private readonly ConcurrentDictionary<int, CellRenderCacheEntry[]> _rows = new();
    private readonly object _sync = new();
    private readonly Typeface _typeface;
    private readonly IBrush _textBrush;
    private readonly GridRendererContext _context;
    private int _windowStart = -1;
    private int _windowEndExclusive = -1;

    public RowRenderCache(Typeface typeface, IBrush textBrush, GridRendererContext context)
    {
        _typeface = typeface;
        _textBrush = textBrush;
        _context = context;
    }

    public bool TryGetRow(int rowIndex, out CellRenderCacheEntry[]? row)
    {
        return _rows.TryGetValue(rowIndex, out row);
    }

    public void Clear()
    {
        lock (_sync)
        {
            _rows.Clear();
            _windowStart = -1;
            _windowEndExclusive = -1;
        }
    }

    public void InvalidateColumn(int columnIndex)
    {
        lock (_sync)
        {
            foreach (var key in _rows.Keys)
            {
                if (_rows.TryGetValue(key, out var row) &&
                    columnIndex >= 0 &&
                    columnIndex < row.Length)
                {
                    row[columnIndex] = null!;
                }
            }
        }
    }

    public void EnsureWindow(
        int topRowIndex,
        int rowCount,
        int columnCount,
        IReadOnlyList<IReadOnlyList<object?>> rows,
        IReadOnlyList<Type> columnTypes,
        Func<int, object?, IGridCellRenderer> getRenderer)
    {
        if (rowCount <= 0 || columnCount <= 0)
            return;

        var start = Math.Max(0, topRowIndex - 40);
        var endExclusive = Math.Min(rowCount, start + 200);

        lock (_sync)
        {
            if (start == _windowStart && endExclusive == _windowEndExclusive)
                return;

            _windowStart = start;
            _windowEndExclusive = endExclusive;
        }

        _ = Task.Run(() => PopulateWindow(start, endExclusive, columnCount, rows, columnTypes, getRenderer));
    }

    private void PopulateWindow(
        int start,
        int endExclusive,
        int columnCount,
        IReadOnlyList<IReadOnlyList<object?>> rows,
        IReadOnlyList<Type> columnTypes,
        Func<int, object?, IGridCellRenderer> getRenderer)
    {
        var next = new Dictionary<int, CellRenderCacheEntry[]>(Math.Max(0, endExclusive - start));
        for (var rowIndex = start; rowIndex < endExclusive; rowIndex++)
        {
            var sourceRow = rows[rowIndex];
            var cells = new CellRenderCacheEntry[columnCount];
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var value = columnIndex < sourceRow.Count ? sourceRow[columnIndex] : null;
                var renderer = getRenderer(columnIndex, value);
                var text = renderer.FormatValue(value, _context);
                cells[columnIndex] = new CellRenderCacheEntry(
                    text,
                    new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _typeface, 12, _textBrush),
                    renderer.Alignment);
            }

            next[rowIndex] = cells;
        }

        lock (_sync)
        {
            if (start != _windowStart || endExclusive != _windowEndExclusive)
                return;

            _rows.Clear();
            foreach (var pair in next)
                _rows[pair.Key] = pair.Value;
        }
    }
}
