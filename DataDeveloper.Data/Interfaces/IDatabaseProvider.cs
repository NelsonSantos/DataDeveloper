using System.Data;
using System.Data.Common;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Interfaces;

public interface IDatabaseProvider
{
    DbConnection GetConnection();
    TestConnectionResult TestConnection();
    IReadOnlyList<string> GetAvailableDatabaseNames();
}

public interface IDatabaseProvider<TConnectionSettings> : IDatabaseProvider 
    where TConnectionSettings : IConnectionSettings
{
    TConnectionSettings ConnectionSettings { get; }
}
