using System.Threading.Tasks;
using Avalonia.Controls;
using DataDeveloper.Models;

namespace DataDeveloper.Interfaces;

public interface IConnectionDialogService
{
    Task<ConnectionModel?> ShowDialogAsync(Window parentWindow);
}