using DataDeveloper.Data.Models;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Data.Providers.PostgresSql;

public class PostgresConnectionSettings : ConnectionSettings
{
    [Reactive] public string Server { get; set; } = string.Empty;
    [Reactive] public string Database { get; set; } = string.Empty;
    [Reactive] public int Port { get; set; } = 5432;
}
