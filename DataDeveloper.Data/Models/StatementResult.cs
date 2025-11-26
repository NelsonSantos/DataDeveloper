using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace DataDeveloper.Data.Models;

public class StatementResult
{
    public StatementResult(DbDataReader dataReader, string statement, Stopwatch watcher)
    {
        DataReader = dataReader;
        Statement = statement;
        Watcher = watcher;
    }
    public DbDataReader DataReader { get; }
    public string Statement { get; }
    public Stopwatch Watcher { get; }
    public async Task CloseDataReader() => await DataReader.CloseAsync();
}