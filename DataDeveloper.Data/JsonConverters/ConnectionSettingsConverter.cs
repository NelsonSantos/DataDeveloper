using System.Text.Json;
using System.Text.Json.Serialization;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.Oracle;
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.PostgresSql;
using DataDeveloper.Data.Providers.SqLite;
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

        ConnectionSettings connection = type switch
        {
            DatabaseType.SqlServer => (ConnectionSettings?)JsonSerializer.Deserialize<SqlServerConnectionSettings>(json, options),
            DatabaseType.Oracle => (ConnectionSettings?)JsonSerializer.Deserialize<OracleConnectionSettings>(json, options),
            DatabaseType.PostgresSql => (ConnectionSettings?)JsonSerializer.Deserialize<PostgresConnectionSettings>(json, options),
            DatabaseType.MySql => (ConnectionSettings?)JsonSerializer.Deserialize<MySqlConnectionSettings>(json, options),
            DatabaseType.SqLite => (ConnectionSettings?)JsonSerializer.Deserialize<SqLiteConnectionSettings>(json, options),
            _ => throw new NotSupportedException($"Tipo {type} não suportado.")
        } ?? throw new JsonException($"Could not deserialize connection settings for database type {type}.");
        return connection;
    }

    public override void Write(Utf8JsonWriter writer, ConnectionSettings value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);
    }
}
