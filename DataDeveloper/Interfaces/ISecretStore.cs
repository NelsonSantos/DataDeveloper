using System.Threading.Tasks;

namespace DataDeveloper.Interfaces;

public interface ISecretStore
{
    bool IsAvailable { get; }
    string? UnavailableReason { get; }
    Task SaveAsync(string key, string secret);
    Task<string?> GetAsync(string key);
    Task DeleteAsync(string key);
}
