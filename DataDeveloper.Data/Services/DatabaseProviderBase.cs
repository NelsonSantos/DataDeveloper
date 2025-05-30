using System.Data;
using System.Text;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Services;

public abstract class DatabaseProviderBase<TConnectionSettings> : IDatabaseProvider<TConnectionSettings> 
    where TConnectionSettings : IConnectionSettings
{
    protected DatabaseProviderBase(TConnectionSettings connectionSettings)
    {
        ConnectionSettings = connectionSettings;
    }

    public TConnectionSettings ConnectionSettings { get; }
    public abstract IDbConnection GetConnection();
    public abstract string GetTableStatement();
    public abstract string GetColumnStatement();

    public TestConnectionResult TestConnection()
    {
        try
        {
            using var conn = GetConnection();
            conn.Open();
            return new TestConnectionResult(true, "Connection successfully established");
        }
        catch (Exception e)
        {
            return new TestConnectionResult(false, e.Message);
        }
    }
}