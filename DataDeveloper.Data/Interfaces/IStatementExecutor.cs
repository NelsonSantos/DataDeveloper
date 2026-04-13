using System.Threading;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Interfaces;

public interface IStatementExecutor
{
    bool HasActiveTransaction { get; }

    Task BeginTransaction(CancellationToken cancellationToken = default);

    Task<IEnumerable<StatementResult>> ExecuteStatement(
        string sqlStatement,
        IReadOnlyDictionary<string, object?>? parameters = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default);

    Task<int> ExecuteCommandInTransaction(
        EditableResultSetCommand command,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default);

    Task CommitTransaction(CancellationToken cancellationToken = default);

    Task RollbackTransaction(CancellationToken cancellationToken = default);

    void Cancel();
}
