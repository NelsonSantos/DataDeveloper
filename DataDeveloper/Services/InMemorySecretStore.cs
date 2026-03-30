using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataDeveloper.Interfaces;

namespace DataDeveloper.Services;

public class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

    public bool IsAvailable => true;
    public string? UnavailableReason => null;

    public Task SaveAsync(string key, string secret)
    {
        _secrets[key] = secret;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key)
    {
        _secrets.TryGetValue(key, out var secret);
        return Task.FromResult<string?>(secret);
    }

    public Task DeleteAsync(string key)
    {
        _secrets.Remove(key);
        return Task.CompletedTask;
    }
}
