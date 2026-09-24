using DataDeveloper.Data.Models;
using ReactiveUI.SourceGenerators;

namespace DataDeveloper.Data.Providers.Oracle;

public partial class OracleConnectionSettings : ConnectionSettings
{
    [Reactive] private string _server = string.Empty;
    [Reactive] private string _database = string.Empty;
    [Reactive] private int _port = 1521;
}
