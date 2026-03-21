using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Data.Models;

public class ConnectionSettings : ReactiveObject, IConnectionSettings
{
    [Reactive] public Guid Id { get; set; }
    [Reactive] public string Name { get; set; } = string.Empty;
    [Reactive] public string User { get; set; } = string.Empty;
    [Reactive] public string Password { get; set; } = string.Empty;
    [Reactive] public bool Encrypt { get; set; } = true;
    [Reactive] public bool TrustServerCertificate { get; set; }
    [Reactive] public bool AllowBlankPassword { get; set; }
    [Reactive] public DatabaseType DatabaseType { get; set; }
}
