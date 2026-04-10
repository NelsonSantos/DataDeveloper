using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace DataDeveloper.Data.Models;

public class StatementResult
{
    private bool _isClosed;
    private bool _cancelRequested;

    public StatementResult(
        DbDataReader? dataReader,
        DbConnection? connection,
        DbCommand? command,
        string statement,
        Stopwatch watcher,
        int? recordsAffected = null)
    {
        DataReader = dataReader;
        Connection = connection;
        Command = command;
        Statement = statement;
        Watcher = watcher;
        RecordsAffected = recordsAffected ?? dataReader?.RecordsAffected ?? 0;
    }

    public DbDataReader? DataReader { get; }
    public DbConnection? Connection { get; }
    public DbCommand? Command { get; }
    public string Statement { get; }
    public Stopwatch Watcher { get; }
    public int RecordsAffected { get; }
    public bool HasRows => DataReader?.HasRows ?? false;
    public bool HasDataReader => DataReader is not null;
    public bool HasResultSet => (DataReader?.FieldCount ?? 0) > 0;

    public void CancelExecution()
    {
        _cancelRequested = true;
        try
        {
            Command?.Cancel();
        }
        catch
        {
            // Providers vary in cancellation support; closing the reader/connection is the fallback.
        }
    }

    public async Task CloseDataReader()
    {
        if (_isClosed)
            return;

        _isClosed = true;

        try
        {
            if (_cancelRequested)
                CancelExecution();

            if (DataReader is not null)
                await DataReader.DisposeAsync();
        }
        catch (Exception) when (_cancelRequested)
        {
            // Some providers surface cancellation faults during reader disposal. Treat them as expected.
        }
        finally
        {
            if (Command is not null)
            {
                try
                {
                    await Command.DisposeAsync();
                }
                catch (Exception) when (_cancelRequested)
                {
                    // Ignore provider cleanup failures after an explicit cancel.
                }
            }

            if (Connection is not null)
            {
                try
                {
                    await Connection.DisposeAsync();
                }
                catch (Exception) when (_cancelRequested)
                {
                    // Ignore provider cleanup failures after an explicit cancel.
                }
            }
        }
    }
}
