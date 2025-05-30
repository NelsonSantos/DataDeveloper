using System.Data;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Interfaces;

public interface IDatabaseProvider
{
    IDbConnection GetConnection();
    TestConnectionResult TestConnection();
    string GetTableStatement();
    string GetColumnStatement();
}

public interface IDatabaseProvider<TConnectionSettings> : IDatabaseProvider 
    where TConnectionSettings : IConnectionSettings
{
    TConnectionSettings ConnectionSettings { get; }
}
