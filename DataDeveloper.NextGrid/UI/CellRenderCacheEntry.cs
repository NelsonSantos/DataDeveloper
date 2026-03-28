using Avalonia.Media;
using DataDeveloper.NextGrid.Renderers;

namespace DataDeveloper.NextGrid.UI;

internal sealed record CellRenderCacheEntry(
    string Text,
    FormattedText FormattedText,
    GridColumnAlignment Alignment);
