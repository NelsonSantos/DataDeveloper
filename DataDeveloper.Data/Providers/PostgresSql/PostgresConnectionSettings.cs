using DataDeveloper.Data.Models;
using ReactiveUI.SourceGenerators;

namespace DataDeveloper.Data.Providers.PostgresSql;

public partial class PostgresConnectionSettings : ConnectionSettings
{
    [Reactive] private string _server = string.Empty;
    [Reactive] private string _database = string.Empty;
    [Reactive] private int _port = 5432;
}
