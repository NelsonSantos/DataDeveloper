using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Data.Models;

public class ConnectionSettings : ReactiveObject, IConnectionSettings
{
    public const int DefaultStatementTimeoutSeconds = 60;

    public static DmlTransactionMode GetDefaultDmlTransactionMode(DatabaseType databaseType)
    {
        return databaseType == DatabaseType.Oracle
            ? DmlTransactionMode.ManualCommitRollback
            : DmlTransactionMode.AutoCommit;
    }

    [Reactive] public Guid Id { get; set; }
    [Reactive] public Guid? GroupId { get; set; }
    [Reactive] public Guid? CredentialId { get; set; }
    [Reactive] public string Name { get; set; } = string.Empty;
    [Reactive] public string User { get; set; } = string.Empty;
    [Reactive] public string Password { get; set; } = string.Empty;
    public bool IsPasswordLoaded { get; set; }
    public string? LoadedPasswordSnapshot { get; set; }
    [Reactive] public bool IsBulkSelected { get; set; }
    [Reactive] public bool Encrypt { get; set; } = true;
    [Reactive] public bool TrustServerCertificate { get; set; }
    [Reactive] public bool AllowBlankPassword { get; set; }
    [Reactive] public int StatementTimeoutSeconds { get; set; } = DefaultStatementTimeoutSeconds;
    [Reactive] public DmlTransactionMode DmlTransactionMode { get; set; }
    [Reactive] public DatabaseType DatabaseType { get; set; }
}
