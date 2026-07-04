using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace DataDeveloper.Helpers;

public static class XmlTextFormatter
{
    public static bool TryFormat(string? text, bool indented, out string result)
    {
        result = text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            var document = XDocument.Parse(text);
            using var writer = new StringWriter();
            document.Save(writer, indented ? SaveOptions.None : SaveOptions.DisableFormatting);
            result = writer.ToString();
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    public static bool IsValidXml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            XDocument.Parse(text);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
