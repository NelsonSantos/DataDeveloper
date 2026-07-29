using System.Threading.Tasks;
using Avalonia.Controls;
using DataDeveloper.Data.Interfaces;

namespace DataDeveloper.Interfaces;

public interface IFileImportDialogService
{
    /// <summary>
    /// Opens the file import wizard. When <paramref name="preselectedConnection"/> is provided
    /// (opened from a connection tab's toolbar), the wizard starts on the file-selection step
    /// with that connection already set; otherwise it starts by asking the user to pick one
    /// (opened from the Tools menu). Returns the connection the import ultimately ran against,
    /// or null if the dialog was cancelled before one was selected.
    /// </summary>
    Task<IConnectionSettings?> ShowDialogAsync(Window parentWindow, IConnectionSettings? preselectedConnection = null);
}
