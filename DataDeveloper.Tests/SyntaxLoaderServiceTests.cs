using Avalonia.Headless.XUnit;
using AvaloniaEdit.Highlighting;
using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public sealed class SyntaxLoaderServiceTests
{
    [AvaloniaFact]
    public void RegisterJsonHighlighting_RegistersJsonDefinition()
    {
        SyntaxLoaderService.RegisterJsonHighlighting();

        var definition = HighlightingManager.Instance.GetDefinition("JSON");

        Assert.NotNull(definition);
    }

    [AvaloniaFact]
    public void RegisterXmlHighlighting_OverridesBuiltInDefinitionWithDarkThemeColors()
    {
        SyntaxLoaderService.RegisterXmlHighlighting();

        var definition = HighlightingManager.Instance.GetDefinition("XML");
        Assert.NotNull(definition);

        // AvaloniaEdit ships a built-in "XML" definition tuned for a light background
        // (e.g. XmlTag = DarkMagenta). Make sure our dark-theme colors actually took over.
        var tagColor = definition!.GetNamedColor("XmlTag");
        Assert.NotNull(tagColor?.Foreground);
        Assert.Equal("#FF569CD6", tagColor!.Foreground!.GetColor(null)!.Value.ToString(), ignoreCase: true);
    }
}
