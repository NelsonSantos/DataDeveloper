using System.Data;
using System.Diagnostics;
using Dapper;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Services;

public class StatementExecutor : IStatementExecutor
{
    private readonly IConnectionSettings _connectionSettings;
    private readonly IDatabaseProvider _databaseProvider;
    
    public StatementExecutor(IConnectionSettings connectionSettings)
    {
        _connectionSettings = connectionSettings;
        _databaseProvider = _connectionSettings.GetDatabaseProvider();
    }

    public async Task<IEnumerable<StatementResult>> ExecuteStatement(string sqlStatement)
    {
        try
        {
            var result = new List<StatementResult>();
            var statements = StatementSplitter.SplitStatements(sqlStatement);

            foreach (var statement in statements)
            {
                var watcher = new Stopwatch();
                watcher.Start();

                var connection = _databaseProvider.GetConnection();
                var reader = await connection.ExecuteReaderAsync(statement, commandType: CommandType.Text);
                
                result.Add(new StatementResult(reader, connection, sqlStatement, watcher));
                
                watcher.Stop();
            }

            return result;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}
