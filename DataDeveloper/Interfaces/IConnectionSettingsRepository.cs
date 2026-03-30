using System.Collections.Generic;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Interfaces;

public interface IConnectionSettingsRepository
{
    IReadOnlyList<ConnectionSettings> LoadAll();
    void LoadPassword(ConnectionSettings connectionSettings);
    void Save(ConnectionSettings connectionSettings);
    void Delete(ConnectionSettings connectionSettings);
    void SaveAll(IEnumerable<ConnectionSettings> connections);
}
