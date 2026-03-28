namespace DataDeveloper.NextGrid;

public sealed class GridColumnLayoutEngine
{
    private readonly double _minimumWidth;
    private readonly List<double> _widths = [];

    public GridColumnLayoutEngine(double minimumWidth)
    {
        if (minimumWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(minimumWidth));

        _minimumWidth = minimumWidth;
    }

    public IReadOnlyList<double> Widths => _widths;

    public void EnsureColumnCount(int columnCount)
    {
        while (_widths.Count < columnCount)
            _widths.Add(_minimumWidth);

        while (_widths.Count > columnCount)
            _widths.RemoveAt(_widths.Count - 1);
    }

    public bool TrackWidth(int columnIndex, double measuredWidth)
    {
        if (columnIndex < 0 || columnIndex >= _widths.Count)
            return false;

        var targetWidth = Math.Max(_minimumWidth, measuredWidth);
        if (targetWidth <= _widths[columnIndex])
            return false;

        _widths[columnIndex] = targetWidth;
        return true;
    }

    public bool SetWidth(int columnIndex, double width)
    {
        if (columnIndex < 0 || columnIndex >= _widths.Count)
            return false;

        var targetWidth = Math.Max(_minimumWidth, width);
        if (Math.Abs(_widths[columnIndex] - targetWidth) < 0.01d)
            return false;

        _widths[columnIndex] = targetWidth;
        return true;
    }

    public double GetColumnStart(int columnIndex)
    {
        var offset = 0d;

        for (var index = 0; index < columnIndex && index < _widths.Count; index++)
            offset += _widths[index];

        return offset;
    }

    public double GetTotalWidth()
    {
        var total = 0d;

        for (var index = 0; index < _widths.Count; index++)
            total += _widths[index];

        return total;
    }

    public int GetColumnIndexAt(double contentOffset)
    {
        if (_widths.Count == 0)
            return -1;

        if (contentOffset <= 0)
            return 0;

        var runningWidth = 0d;
        for (var index = 0; index < _widths.Count; index++)
        {
            runningWidth += _widths[index];
            if (contentOffset < runningWidth)
                return index;
        }

        return _widths.Count - 1;
    }

    public double GetHorizontalOffsetForCell(
        double currentOffset,
        double viewportContentWidth,
        int columnIndex,
        bool movedLeft,
        bool movedRight)
    {
        if (columnIndex < 0 || columnIndex >= _widths.Count)
            return Math.Max(0, currentOffset);

        if (viewportContentWidth <= 0)
            return Math.Max(0, currentOffset);

        var cellLeft = GetColumnStart(columnIndex);
        var cellRight = cellLeft + _widths[columnIndex];
        var cellWidth = _widths[columnIndex];
        var leftVisible = Math.Max(0, currentOffset);
        var rightVisible = leftVisible + viewportContentWidth;

        if (movedLeft && cellLeft < leftVisible)
        {
            return cellWidth > viewportContentWidth
                ? Math.Max(0, cellRight - viewportContentWidth)
                : Math.Max(0, cellLeft);
        }

        if (movedRight && cellRight > rightVisible)
        {
            return cellWidth > viewportContentWidth
                ? Math.Max(0, cellLeft)
                : Math.Max(0, cellRight - viewportContentWidth);
        }

        if (cellRight <= leftVisible)
            return Math.Max(0, cellLeft);

        if (cellLeft >= rightVisible)
            return Math.Max(0, cellRight - viewportContentWidth);

        return leftVisible;
    }
}
