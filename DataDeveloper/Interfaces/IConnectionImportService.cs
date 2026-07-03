using System.Threading.Tasks;

namespace DataDeveloper.Interfaces;

public interface IConnectionImportService
{
    Task<int> ImportAsync(string filePath);
}
