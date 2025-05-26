using System.Threading.Tasks;
using Avalonia.Controls;
using DataDeveloper.Models;

namespace DataDeveloper.Services;

public interface IConnectionDialogService
{
    Task<ConnectionModel?> ShowDialogAsync(Window parentWindow);
}