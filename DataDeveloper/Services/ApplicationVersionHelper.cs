using System.Reflection;

namespace DataDeveloper.Services;

public static class ApplicationVersionHelper
{
    public static string GetCurrentVersion(Assembly assembly)
    {
        return assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?
            .Split('+')[0]
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}
