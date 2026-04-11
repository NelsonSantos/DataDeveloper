using System.Threading;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Interfaces;

public interface IStatementExecutor
{
    Task<IEnumerable<StatementResult>> ExecuteStatement(
        string sqlStatement,
        IReadOnlyDictionary<string, object?>? parameters = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default);

    void Cancel();
}
