using System.Xml;
using System.Xml.Linq;

namespace DataDeveloper.NextGrid.Renderers;

public static class XmlTextSniffer
{
    public static bool IsLikelyXml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        if (trimmed.Length < 3 || trimmed[0] != '<' || trimmed[^1] != '>')
            return false;

        try
        {
            XDocument.Parse(trimmed);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
