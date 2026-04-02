using System.Threading;
using System.Threading.Tasks;

namespace DataDeveloper.Interfaces;

public interface IReleaseUpdateService
{
    Task NotifyIfUpdateAvailableAsync(CancellationToken cancellationToken = default);
    Task CheckForUpdatesAsync(CancellationToken cancellationToken = default);
}
