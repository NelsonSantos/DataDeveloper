using DataDeveloper.Data.Models;
using ReactiveUI.SourceGenerators;

namespace DataDeveloper.Data.Providers.SqlServer;

public partial class SqlServerConnectionSettings : ConnectionSettings
{
    [Reactive] private string _server = string.Empty;
    [Reactive] private string _database = string.Empty;
    [Reactive] private int _port = 1433;
    [Reactive] private SqlServerAuthenticationMode _authenticationMode = SqlServerAuthenticationMode.SqlLogin;
}
