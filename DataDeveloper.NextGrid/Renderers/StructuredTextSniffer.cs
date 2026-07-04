namespace DataDeveloper.NextGrid.Renderers;

public static class StructuredTextSniffer
{
    public static StructuredTextKind Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return StructuredTextKind.None;

        var trimmed = text.Trim();
        var firstChar = trimmed[0];

        if ((firstChar == '{' || firstChar == '[') && JsonTextSniffer.IsLikelyJson(trimmed))
            return StructuredTextKind.Json;

        if (firstChar == '<' && XmlTextSniffer.IsLikelyXml(trimmed))
            return StructuredTextKind.Xml;

        return StructuredTextKind.None;
    }
}
