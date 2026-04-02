using System.Threading.Tasks;
using DataDeveloper.Enums;
using DataDeveloper.Views;

namespace DataDeveloper.Interfaces;

public interface IDialogService
{
    Task<DialogResult> ShowDialogAsync(string message, string? title = null, DialogButtons buttons = DialogButtons.Ok, DialogIcon icon = DialogIcon.Info);
    Task<DialogResult> ShowDialogResult(string message, string? title = null);
    Task ShowMessageAsync(string message, string? title = null);
    Task<DialogResult> ShowReleaseUpdateAsync(string message, string? title = null);
    Task<string?> ShowSaveFileDialogAsync(string? suggestedName = null, string? title = null);
    Task<string?> ShowOpenFileAsync(string? title = null);
    Task<string?> ShowOpenDatabaseFileAsync(string? title = null);
    Task<string?> ShowCreateDatabaseFileAsync(string? suggestedName = null, string? title = null);
}
