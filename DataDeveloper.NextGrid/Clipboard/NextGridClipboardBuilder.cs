using System.Collections.ObjectModel;
using System.Text;
using DataDeveloper.NextGrid.Renderers;

namespace DataDeveloper.NextGrid.Clipboard;

internal sealed class NextGridClipboardBuilder
{
    private readonly GridRendererRegistry _rendererRegistry;

    public NextGridClipboardBuilder(GridRendererRegistry rendererRegistry)
    {
        _rendererRegistry = rendererRegistry;
    }

    public string Build(
        IReadOnlyList<GridSelectionRange> ranges,
        ObservableCollection<IReadOnlyList<object?>> rows,
        ObservableCollection<Type> columnTypes)
    {
        if (ranges.Count == 0 || rows.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        var firstRange = true;

        foreach (var range in ranges)
        {
            if (!firstRange)
                builder.AppendLine();

            AppendRange(builder, range, rows, columnTypes);
            firstRange = false;
        }

        return builder.ToString();
    }

    private void AppendRange(
        StringBuilder builder,
        GridSelectionRange range,
        ObservableCollection<IReadOnlyList<object?>> rows,
        ObservableCollection<Type> columnTypes)
    {
        var maxRow = Math.Min(range.BottomRow, rows.Count - 1);

        for (var rowIndex = Math.Max(0, range.TopRow); rowIndex <= maxRow; rowIndex++)
        {
            if (rowIndex > Math.Max(0, range.TopRow))
                builder.AppendLine();

            var row = rows[rowIndex];
            for (var columnIndex = Math.Max(0, range.LeftColumn); columnIndex <= range.RightColumn; columnIndex++)
            {
                if (columnIndex > Math.Max(0, range.LeftColumn))
                    builder.Append('\t');

                object? value = columnIndex < row.Count ? row[columnIndex] : null;
                var valueType = columnIndex < columnTypes.Count ? columnTypes[columnIndex] : value?.GetType();
                var renderer = _rendererRegistry.Resolve(valueType, value);
                var text = renderer.FormatValue(value, GridRendererContext.Default);
                builder.Append(text.Replace("\r\n", "\n").Replace('\r', '\n'));
            }
        }
    }
}
