using System.Data.Common;
using DataDeveloper.Data.Services;
using Npgsql;

namespace DataDeveloper.Data.Providers.PostgresSql;

public class PostgresDatabaseProvider : DatabaseProviderBase<PostgresConnectionSettings>
{
    public PostgresDatabaseProvider(PostgresConnectionSettings connectionSettings)
        : base(connectionSettings)
    {
    }

    public override DbConnection GetConnection()
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = ConnectionSettings.Server,
            Database = ConnectionSettings.Database,
            Username = ConnectionSettings.User,
            Password = ConnectionSettings.Password,
            Port = ConnectionSettings.Port,
            SslMode = GetSslMode()
        };

        return new NpgsqlConnection(connectionStringBuilder.ConnectionString);
    }

    public override IReadOnlyList<string> GetAvailableDatabaseNames()
    {
        using var connection = new NpgsqlConnection(BuildConnectionString("postgres"));
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              select datname
                              from pg_database
                              where datallowconn
                                and not datistemplate
                              order by datname;
                              """;
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));
        return names;
    }

    private string BuildConnectionString(string? databaseName)
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = ConnectionSettings.Server,
            Database = string.IsNullOrWhiteSpace(databaseName) ? "postgres" : databaseName,
            Username = ConnectionSettings.User,
            Password = ConnectionSettings.Password,
            Port = ConnectionSettings.Port,
            SslMode = GetSslMode()
        };

        return connectionStringBuilder.ConnectionString;
    }

    /// <summary>
    /// Encryption without certificate validation when the server certificate is trusted (Npgsql's own
    /// TrustServerCertificate option is obsolete: Require already skips validation).
    /// </summary>
    private SslMode GetSslMode() =>
        ConnectionSettings.Encrypt
            ? ConnectionSettings.TrustServerCertificate ? SslMode.Require : SslMode.VerifyCA
            : SslMode.Disable;
}
