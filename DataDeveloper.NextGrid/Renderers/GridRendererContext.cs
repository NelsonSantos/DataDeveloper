using System.Globalization;

namespace DataDeveloper.NextGrid.Renderers;

public readonly record struct GridRendererContext(CultureInfo Culture)
{
    public static GridRendererContext Default { get; } = new(CultureInfo.CurrentCulture);
}
