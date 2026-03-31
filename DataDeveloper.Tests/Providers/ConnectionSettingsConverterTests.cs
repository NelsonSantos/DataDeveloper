using System.Text.Json;
using System.Text.Json.Serialization;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.JsonConverters;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.PostgresSql;
using DataDeveloper.Data.Providers.SqlServer;
using Xunit;

namespace DataDeveloper.Tests.Providers;

public class ConnectionSettingsConverterTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter(), new ConnectionSettingsConverter() }
    };

    [Fact]
    public void Deserialize_ReturnsSqlServerConnectionSettings_ForSqlServerPayload()
    {
        const string json = """
                            {
                              "Id":"11111111-1111-1111-1111-111111111111",
                              "Name":"SqlServer",
                              "DatabaseType":"SqlServer",
                              "Server":"localhost",
                              "Database":"master",
                              "User":"sa",
                              "Password":"pwd",
                              "Encrypt":true,
                              "TrustServerCertificate":false
                            }
                            """;

        var connection = JsonSerializer.Deserialize<ConnectionSettings>(json, SerializerOptions);

        var typed = Assert.IsType<SqlServerConnectionSettings>(connection);
        Assert.Equal(DatabaseType.SqlServer, typed.DatabaseType);
        Assert.Equal("localhost", typed.Server);
    }

    [Fact]
    public void Deserialize_ReturnsMySqlConnectionSettings_ForMySqlPayload()
    {
        const string json = """
                            {
                              "Id":"22222222-2222-2222-2222-222222222222",
                              "Name":"MySql",
                              "DatabaseType":"MySql",
                              "Server":"localhost",
                              "Database":"app",
                              "User":"root",
                              "Password":"pwd",
                              "Port":3307,
                              "Encrypt":false,
                              "TrustServerCertificate":true
                            }
                            """;

        var connection = JsonSerializer.Deserialize<ConnectionSettings>(json, SerializerOptions);

        var typed = Assert.IsType<MySqlConnectionSettings>(connection);
        Assert.Equal(DatabaseType.MySql, typed.DatabaseType);
        Assert.Equal((uint)3307, typed.Port);
    }

    [Fact]
    public void Deserialize_ReturnsPostgresConnectionSettings_ForPostgresPayload()
    {
        const string json = """
                            {
                              "Id":"25222222-2222-2222-2222-222222222222",
                              "Name":"Postgres",
                              "DatabaseType":"PostgresSql",
                              "Server":"localhost",
                              "Database":"app",
                              "User":"postgres",
                              "Password":"pwd",
                              "Port":5433,
                              "Encrypt":true,
                              "TrustServerCertificate":false
                            }
                            """;

        var connection = JsonSerializer.Deserialize<ConnectionSettings>(json, SerializerOptions);

        var typed = Assert.IsType<PostgresConnectionSettings>(connection);
        Assert.Equal(DatabaseType.PostgresSql, typed.DatabaseType);
        Assert.Equal(5433, typed.Port);
    }

    [Fact]
    public void Serialize_RoundTripsSqlServerConnectionSettings()
    {
        ConnectionSettings connection = new SqlServerConnectionSettings
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "SqlServer",
            DatabaseType = DatabaseType.SqlServer,
            Server = "localhost",
            Database = "master",
            User = "sa",
            Password = "pwd",
            Encrypt = true,
            TrustServerCertificate = false
        };

        var json = JsonSerializer.Serialize(connection, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<ConnectionSettings>(json, SerializerOptions);

        var typed = Assert.IsType<SqlServerConnectionSettings>(deserialized);
        Assert.Equal("localhost", typed.Server);
        Assert.Equal(DatabaseType.SqlServer, typed.DatabaseType);
    }

    [Fact]
    public void Serialize_RoundTripsMySqlConnectionSettings()
    {
        ConnectionSettings connection = new MySqlConnectionSettings
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Name = "MySql",
            DatabaseType = DatabaseType.MySql,
            Server = "localhost",
            Database = "app",
            User = "root",
            Password = "pwd",
            Port = 3306,
            Encrypt = false,
            TrustServerCertificate = true
        };

        var json = JsonSerializer.Serialize(connection, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<ConnectionSettings>(json, SerializerOptions);

        var typed = Assert.IsType<MySqlConnectionSettings>(deserialized);
        Assert.Equal((uint)3306, typed.Port);
        Assert.Equal(DatabaseType.MySql, typed.DatabaseType);
    }

    [Fact]
    public void Serialize_RoundTripsPostgresConnectionSettings()
    {
        ConnectionSettings connection = new PostgresConnectionSettings
        {
            Id = Guid.Parse("54444444-4444-4444-4444-444444444444"),
            Name = "Postgres",
            DatabaseType = DatabaseType.PostgresSql,
            Server = "localhost",
            Database = "app",
            User = "postgres",
            Password = "pwd",
            Port = 5432,
            Encrypt = true,
            TrustServerCertificate = false
        };

        var json = JsonSerializer.Serialize(connection, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<ConnectionSettings>(json, SerializerOptions);

        var typed = Assert.IsType<PostgresConnectionSettings>(deserialized);
        Assert.Equal(5432, typed.Port);
        Assert.Equal(DatabaseType.PostgresSql, typed.DatabaseType);
    }
}
