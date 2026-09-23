using System.Data.Common;
using DataDeveloper.Data.Services;
using Microsoft.Data.Sqlite;

namespace DataDeveloper.Data.Providers.SqLite;

public class SqLiteDatabaseProvider : DatabaseProviderBase<SqLiteConnectionSettings>
{
    public SqLiteDatabaseProvider(SqLiteConnectionSettings connectionSettings)
        : base(connectionSettings)
    {
    }

    public override DbConnection GetConnection()
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = ConnectionSettings.Database
        };

        return new SqliteConnection(connectionStringBuilder.ConnectionString);
    }
}
