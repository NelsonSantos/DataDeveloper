using System.Data;
using System.Data.Common;

namespace DataDeveloper.Data.Models;

public class StatementResult
{
    public StatementResult(DbDataReader dataReader, string statement)
    {
        DataReader = dataReader;
        Statement = statement;
    }
    public DbDataReader DataReader { get; }
    public string Statement { get; }
    public async Task CloseDataReader() => await DataReader.CloseAsync();
}