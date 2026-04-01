using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace DataDeveloper.Data.Models;

public class StatementResult
{
    private bool _isClosed;

    public StatementResult(DbDataReader? dataReader, DbConnection? connection, string statement, Stopwatch watcher, int? recordsAffected = null)
    {
        DataReader = dataReader;
        Connection = connection;
        Statement = statement;
        Watcher = watcher;
        RecordsAffected = recordsAffected ?? dataReader?.RecordsAffected ?? 0;
    }

    public DbDataReader? DataReader { get; }
    public DbConnection? Connection { get; }
    public string Statement { get; }
    public Stopwatch Watcher { get; }
    public int RecordsAffected { get; }
    public bool HasRows => DataReader?.HasRows ?? false;
    public bool HasDataReader => DataReader is not null;
    public bool HasResultSet => (DataReader?.FieldCount ?? 0) > 0;

    public async Task CloseDataReader()
    {
        if (_isClosed)
            return;

        _isClosed = true;

        try
        {
            if (DataReader is not null)
                await DataReader.DisposeAsync();
        }
        finally
        {
            if (Connection is not null)
                await Connection.DisposeAsync();
        }
    }
}
