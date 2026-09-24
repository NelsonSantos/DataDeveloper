using DataDeveloper.Data.Models;
using ReactiveUI.SourceGenerators;

namespace DataDeveloper.Data.Providers.SqLite;

public partial class SqLiteConnectionSettings : ConnectionSettings
{
    [Reactive] private string _database = string.Empty;
}
