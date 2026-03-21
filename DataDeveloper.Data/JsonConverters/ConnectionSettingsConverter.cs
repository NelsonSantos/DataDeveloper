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
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var type = Enum.Parse<DatabaseType>(root.GetProperty("DatabaseType").ToString());

        var json = root.GetRawText();

        var connection = type switch
        {
            DatabaseType.SqlServer => JsonSerializer.Deserialize<SqlServerConnectionSettings>(json, options),
            _ => throw new NotSupportedException($"Tipo {type} não suportado.")
        } ?? throw new JsonException($"Could not deserialize connection settings for database type {type}.");

        ApplyLegacyTlsDefaults(root, connection);
        return connection;
    }

    public override void Write(Utf8JsonWriter writer, ConnectionSettings value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);
    }

    private static void ApplyLegacyTlsDefaults(JsonElement root, ConnectionSettings? connection)
    {
        if (connection is null)
            return;

        if (!root.TryGetProperty("Encrypt", out _) && !root.TryGetProperty("encrypt", out _))
            connection.Encrypt = true;

        if (root.TryGetProperty("UseTrustedConnection", out var legacyTrustedConnection) ||
            root.TryGetProperty("useTrustedConnection", out legacyTrustedConnection))
        {
            connection.TrustServerCertificate = legacyTrustedConnection.GetBoolean();
            return;
        }

        if (!root.TryGetProperty("TrustServerCertificate", out _) &&
            !root.TryGetProperty("trustServerCertificate", out _))
        {
            connection.TrustServerCertificate = true;
        }
    }
}
