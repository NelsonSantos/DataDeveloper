using System.Data.Common;
using DataDeveloper.Data.Services;
using MySqlConnector;

namespace DataDeveloper.Data.Providers.MySql;

public class MySqlDatabaseProvider : DatabaseProviderBase<MySqlConnectionSettings>
{
    public MySqlDatabaseProvider(MySqlConnectionSettings connectionSettings)
        : base(connectionSettings)
    {
    }

    public override DbConnection GetConnection()
    {
        var sslMode = ConnectionSettings.Encrypt
            ? ConnectionSettings.TrustServerCertificate
                ? MySqlSslMode.Required
                : MySqlSslMode.VerifyCA
            : MySqlSslMode.None;

        var connectionStringBuilder = new MySqlConnectionStringBuilder
        {
            Server = ConnectionSettings.Server,
            Database = ConnectionSettings.Database,
            UserID = ConnectionSettings.User,
            Password = ConnectionSettings.Password,
            Port = ConnectionSettings.Port,
            SslMode = sslMode,
            AllowPublicKeyRetrieval = true
        };

        return new MySqlConnection(connectionStringBuilder.ConnectionString);
    }

    public override IReadOnlyList<string> GetAvailableDatabaseNames()
    {
        using var connection = new MySqlConnection(BuildConnectionString(string.Empty));
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              show databases;
                              """;
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));
        return names;
    }

    private string BuildConnectionString(string? databaseName)
    {
        var sslMode = ConnectionSettings.Encrypt
            ? ConnectionSettings.TrustServerCertificate
                ? MySqlSslMode.Required
                : MySqlSslMode.VerifyCA
            : MySqlSslMode.None;

        var connectionStringBuilder = new MySqlConnectionStringBuilder
        {
            Server = ConnectionSettings.Server,
            Database = databaseName,
            UserID = ConnectionSettings.User,
            Password = ConnectionSettings.Password,
            Port = ConnectionSettings.Port,
            SslMode = sslMode,
            AllowPublicKeyRetrieval = true
        };

        return connectionStringBuilder.ConnectionString;
    }
}
