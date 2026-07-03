using System.Collections.Generic;
using System.Threading.Tasks;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Interfaces;

public interface IConnectionExportService
{
    Task ExportAsync(
        IReadOnlyList<ConnectionSettings> connections,
        string filePath,
        bool includePasswords);
}
