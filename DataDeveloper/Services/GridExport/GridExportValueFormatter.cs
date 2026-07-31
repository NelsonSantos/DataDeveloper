using System;
using DataDeveloper.NextGrid.Renderers;

namespace DataDeveloper.Services.GridExport;

/// <summary>
/// Turns a raw grid cell value into export-ready text. Delegates to <see cref="GridRendererRegistry"/>
/// for the same formatting already shown on screen and used by copy-to-clipboard (dates, numbers,
/// nulls), except for <c>byte[]</c>: the registry's catch-all renderer collapses it to the useless
/// "System.Byte[]" text, which is fine for a display cell but throws away real data on export.
/// </summary>
public sealed class GridExportValueFormatter
{
    private readonly GridRendererRegistry _rendererRegistry;

    public GridExportValueFormatter(GridRendererRegistry rendererRegistry)
    {
        _rendererRegistry = rendererRegistry;
    }

    public string Format(object? value, Type? valueType)
    {
        if (value is byte[] bytes)
            return "0x" + Convert.ToHexString(bytes);

        var renderer = _rendererRegistry.Resolve(valueType, value);
        return renderer.FormatValue(value, GridRendererContext.Default);
    }
}
