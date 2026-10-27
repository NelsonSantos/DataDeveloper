using System.Threading.Tasks;
using Avalonia.Controls;

namespace DataDeveloper.Interfaces;

public interface IPreferencesDialogService
{
    Task ShowAsync(Window? parentWindow);
}
