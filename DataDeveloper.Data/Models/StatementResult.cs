using System.Data;
using System.Data.Common;

namespace DataDeveloper.Data.Models;

public class StatementResult
{
    public StatementResult(DbDataReader dataReader)
    {
        DataReader = dataReader;
    }
    public DbDataReader DataReader { get; }
    public async Task CloseDataReader() => await DataReader.CloseAsync();
}