using System.Data.Common;
using DataDeveloper.Data.Services;
using Oracle.ManagedDataAccess.Client;

namespace DataDeveloper.Data.Providers.Oracle;

public class OracleDatabaseProvider : DatabaseProviderBase<OracleConnectionSettings>
{
    public OracleDatabaseProvider(OracleConnectionSettings connectionSettings)
        : base(connectionSettings)
    {
    }

    public override DbConnection GetConnection()
    {
        var connectionStringBuilder = new OracleConnectionStringBuilder
        {
            UserID = ConnectionSettings.User,
            Password = ConnectionSettings.Password,
            DataSource = $"{ConnectionSettings.Server}:{ConnectionSettings.Port}/{ConnectionSettings.Database}"
        };

        return new OracleConnection(connectionStringBuilder.ConnectionString);
    }
}
