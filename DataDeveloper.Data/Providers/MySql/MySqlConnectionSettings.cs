using DataDeveloper.Data.Models;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Data.Providers.MySql;

public class MySqlConnectionSettings : ConnectionSettings
{
    [Reactive] public string Server { get; set; } = string.Empty;
    [Reactive] public string Database { get; set; } = string.Empty;
    [Reactive] public uint Port { get; set; } = 3306;
}
