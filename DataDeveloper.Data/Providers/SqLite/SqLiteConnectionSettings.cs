using DataDeveloper.Data.Models;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Data.Providers.SqLite;

public class SqLiteConnectionSettings : ConnectionSettings
{
    [Reactive] public string Database { get; set; } = string.Empty;
}
