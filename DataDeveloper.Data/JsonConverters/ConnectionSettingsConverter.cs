using System.Text.Json;
using System.Text.Json.Serialization;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.SqlServer;

namespace DataDeveloper.Data.JsonConverters;

public class ConnectionSettingsConverter : JsonConverter<ConnectionSettings>
{
    public override ConnectionSettings Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Clona o JSON
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var type = Enum.Parse<DatabaseType>(root.GetProperty("DatabaseType").ToString());

        var json = root.GetRawText();

        return type switch
        {
            DatabaseType.SqlServer => JsonSerializer.Deserialize<SqlServerConnectionSettings>(json, options),
            _ => throw new NotSupportedException($"Tipo {type} não suportado.")
        };
    }

    public override void Write(Utf8JsonWriter writer, ConnectionSettings value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);
    }
}
