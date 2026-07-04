using System.Text.Json;

namespace DataDeveloper.Helpers;

public static class JsonTextFormatter
{
    public static bool TryFormat(string? text, bool indented, out string result)
    {
        result = text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            using var document = JsonDocument.Parse(text);
            result = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = indented });
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool IsValidJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            using var _ = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
