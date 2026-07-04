using System;
using AvaloniaEdit.Highlighting;
using System.Xml;
using System.Reflection;
using Avalonia.Platform;
using AvaloniaEdit.Highlighting.Xshd;

namespace DataDeveloper.Services;

public static class SyntaxLoaderService
{
    public static void RegisterSqlHighlighting()
    {
        // var assembly = Assembly.GetExecutingAssembly();
        // using var stream = assembly.GetManifestResourceStream("DataDeveloper.Assets.Syntax.TSQL-Mode.xshd");
        var uri = new Uri("avares://DataDeveloper/Assets/Syntax/TSQL-Mode.xshd");
        using var stream = AssetLoader.Open(uri);        
        using var reader = new XmlTextReader(stream);

        var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        HighlightingManager.Instance.RegisterHighlighting("SQL", new[] { ".sql" }, highlighting);
    }

    public static void RegisterJsonHighlighting()
    {
        var uri = new Uri("avares://DataDeveloper/Assets/Syntax/Json.xshd");
        using var stream = AssetLoader.Open(uri);
        using var reader = new XmlTextReader(stream);

        var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        HighlightingManager.Instance.RegisterHighlighting("JSON", new[] { ".json" }, highlighting);
    }

    public static void RegisterXmlHighlighting()
    {
        var uri = new Uri("avares://DataDeveloper/Assets/Syntax/Xml.xshd");
        using var stream = AssetLoader.Open(uri);
        using var reader = new XmlTextReader(stream);

        var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        HighlightingManager.Instance.RegisterHighlighting("XML", new[] { ".xml" }, highlighting);
    }
}
