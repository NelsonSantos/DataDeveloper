using System.Threading.Tasks;
using DataDeveloper.Enums;
using DataDeveloper.Views;

namespace DataDeveloper.Interfaces;

public interface IDialogService
{
    Task<DialogResult> ShowDialogResult(string message, string? title = null);
    Task ShowMessageAsync(string message, string? title = null);
    Task<string?> ShowSaveFileDialogAsync(string? suggestedName = null, string? title = null);
    Task<string?> ShowOpenFileAsync(string? title = null);
}