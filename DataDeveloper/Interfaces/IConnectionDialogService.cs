using System.Threading.Tasks;
using Avalonia.Controls;
using DataDeveloper.Data.Interfaces;

namespace DataDeveloper.Interfaces;

public interface IConnectionDialogService
{
    Task<IConnectionSettings?> ShowDialogAsync(Window parentWindow);
}