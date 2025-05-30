using DataDeveloper.Data.Models;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Data.Providers.SqlServer;

public class SqlServerConnectionSettings : ConnectionSettings
{
    [Reactive]public string Server { get; set; }
    [Reactive]public string Database { get; set; }
}