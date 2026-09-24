using DataDeveloper.Data.Models;
using ReactiveUI.SourceGenerators;

namespace DataDeveloper.Data.Providers.MySql;

public partial class MySqlConnectionSettings : ConnectionSettings
{
    [Reactive] private string _server = string.Empty;
    [Reactive] private string _database = string.Empty;
    [Reactive] private uint _port = 3306;
}
