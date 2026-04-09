using DataDeveloper.Data.Models;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Data.Providers.SqlServer;

public class SqlServerConnectionSettings : ConnectionSettings
{
    [Reactive]public string Server { get; set; } = string.Empty;
    [Reactive]public string Database { get; set; } = string.Empty;
    [Reactive]public SqlServerAuthenticationMode AuthenticationMode { get; set; } = SqlServerAuthenticationMode.SqlLogin;
}
