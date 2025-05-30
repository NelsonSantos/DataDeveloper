using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Data.Models;

public class ConnectionSettings : ReactiveObject, IConnectionSettings
{
    [Reactive] public Guid Id { get; set; }
    [Reactive] public string Name { get; set; }
    [Reactive] public string User { get; set; }
    [Reactive] public string Password { get; set; }
    [Reactive] public bool UseTrustedConnection { get; set; } = true;
    [Reactive] public bool AllowBlankPassword { get; set; }
    [Reactive] public DatabaseType DatabaseType { get; set; }
}