using System;
using AvaloniaEdit.Highlighting;
using System.Xml;
using System.Reflection;
using Avalonia.Platform;
using AvaloniaEdit.Highlighting.Xshd;

namespace DataDeveloper.Services;

public static class SyntaxLoader
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
}
