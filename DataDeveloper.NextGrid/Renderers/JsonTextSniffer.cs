using System.Text.Json;

namespace DataDeveloper.NextGrid.Renderers;

public static class JsonTextSniffer
{
    public static bool IsLikelyJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        if (trimmed.Length < 2)
            return false;

        var firstChar = trimmed[0];
        var lastChar = trimmed[^1];
        var looksLikeObject = firstChar == '{' && lastChar == '}';
        var looksLikeArray = firstChar == '[' && lastChar == ']';
        if (!looksLikeObject && !looksLikeArray)
            return false;

        try
        {
            using var _ = JsonDocument.Parse(trimmed);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
