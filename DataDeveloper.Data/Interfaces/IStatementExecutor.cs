using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Interfaces;

public interface IStatementExecutor
{
    Task<IEnumerable<StatementResult>> ExecuteStatement(string sqlStatement);
}