using DataDeveloper.Data.Models;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Data.Providers.Oracle;

public class OracleConnectionSettings : ConnectionSettings
{
    [Reactive] public string Server { get; set; } = string.Empty;
    [Reactive] public string Database { get; set; } = string.Empty;
    [Reactive] public int Port { get; set; } = 1521;
}
