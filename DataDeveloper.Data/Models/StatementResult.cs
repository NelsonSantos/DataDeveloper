using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace DataDeveloper.Data.Models;

public class StatementResult
{
    private bool _isClosed;

    public StatementResult(DbDataReader dataReader, DbConnection connection, string statement, Stopwatch watcher)
    {
        DataReader = dataReader;
        Connection = connection;
        Statement = statement;
        Watcher = watcher;
    }

    public DbDataReader DataReader { get; }
    public DbConnection Connection { get; }
    public string Statement { get; }
    public Stopwatch Watcher { get; }

    public async Task CloseDataReader()
    {
        if (_isClosed)
            return;

        _isClosed = true;

        try
        {
            await DataReader.DisposeAsync();
        }
        finally
        {
            await Connection.DisposeAsync();
        }
    }
}
