using System.Threading.Tasks;
using Avalonia.Controls;

namespace DataDeveloper.Interfaces;

public sealed record ExportConnectionsOptionsResult(bool IncludePasswords);

public interface IExportConnectionsOptionsDialogService
{
    Task<ExportConnectionsOptionsResult?> ShowDialogAsync(Window parentWindow);
}
