using System.Threading.Tasks;
using Avalonia.Controls;
using DataDeveloper.NextGrid.Renderers;

namespace DataDeveloper.Interfaces;

public interface IStructuredTextCellDialogService
{
    Task<string?> ShowDialogAsync(Window parentWindow, string? value, bool isEditable, StructuredTextKind kind);
}
