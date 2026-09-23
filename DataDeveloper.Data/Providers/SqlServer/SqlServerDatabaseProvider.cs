using System.Data;
using System.Data.Common;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Services;
using Microsoft.Data.SqlClient;

namespace DataDeveloper.Data.Providers.SqlServer;

public class SqlServerDatabaseProvider : DatabaseProviderBase<SqlServerConnectionSettings>
{
    public SqlServerDatabaseProvider(SqlServerConnectionSettings connectionSettings) 
        : base(connectionSettings)
    {
    }

    public override DbConnection GetConnection()
    {
        return new SqlConnection(BuildConnectionString(ConnectionSettings.Database));
    }

    public override IReadOnlyList<string> GetAvailableDatabaseNames()
    {
        using var connection = new SqlConnection(BuildConnectionString("master"));
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              select name
                              from sys.databases
                              where state = 0
                                and has_dbaccess(name) = 1
                              order by name;
                              """;
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));
        return names;
    }

    
    private string BuildConnectionString(string? databaseName)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = BuildDataSource(),
            InitialCatalog = string.IsNullOrWhiteSpace(databaseName) ? "master" : databaseName,
            Encrypt = ConnectionSettings.Encrypt,
            TrustServerCertificate = ConnectionSettings.TrustServerCertificate
        };

        if (ConnectionSettings.AuthenticationMode == SqlServerAuthenticationMode.WindowsIntegrated)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = ConnectionSettings.User;
            builder.Password = ConnectionSettings.Password;
        }

        return builder.ConnectionString;
    }

    private string BuildDataSource()
    {
        if (string.IsNullOrWhiteSpace(ConnectionSettings.Server))
            return ConnectionSettings.Server;

        if (ConnectionSettings.Server.Contains(",", StringComparison.Ordinal) ||
            ConnectionSettings.Server.Contains(":", StringComparison.Ordinal) ||
            ConnectionSettings.Server.Contains("\\", StringComparison.Ordinal))
        {
            return ConnectionSettings.Server;
        }

        return ConnectionSettings.Port > 0
            ? $"{ConnectionSettings.Server},{ConnectionSettings.Port}"
            : ConnectionSettings.Server;
    }
}
