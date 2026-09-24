using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using ReactiveUI;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

namespace DataDeveloper.Data.Models;

public partial class ConnectionSettings : ReactiveObject, IConnectionSettings
{
    public const int DefaultStatementTimeoutSeconds = 60;

    public static DmlTransactionMode GetDefaultDmlTransactionMode(DatabaseType databaseType)
    {
        return databaseType == DatabaseType.Oracle
            ? DmlTransactionMode.ManualCommitRollback
            : DmlTransactionMode.AutoCommit;
    }

    [Reactive] private Guid _id;
    [Reactive] private Guid? _groupId;
    [Reactive] private Guid? _credentialId;
    [Reactive] private string _name = string.Empty;
    [Reactive] private string _user = string.Empty;
    [Reactive] private string _password = string.Empty;
    public bool IsPasswordLoaded { get; set; }
    public string? LoadedPasswordSnapshot { get; set; }
    [Reactive] private bool _isBulkSelected;
    [Reactive] private bool _encrypt = true;
    [Reactive] private bool _trustServerCertificate;
    [Reactive] private bool _allowBlankPassword;
    [Reactive] private int _statementTimeoutSeconds = DefaultStatementTimeoutSeconds;
    [Reactive] private DmlTransactionMode _dmlTransactionMode;
    [Reactive] private DatabaseType _databaseType;
}
