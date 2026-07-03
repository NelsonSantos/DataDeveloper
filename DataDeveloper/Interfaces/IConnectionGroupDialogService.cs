using System.Threading.Tasks;
using Avalonia.Controls;

namespace DataDeveloper.Interfaces;

public interface IConnectionGroupDialogService
{
    Task ShowDialogAsync(Window parentWindow);
}
