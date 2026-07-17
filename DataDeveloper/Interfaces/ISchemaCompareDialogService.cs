using System.Threading.Tasks;
using Avalonia.Controls;

namespace DataDeveloper.Interfaces;

public interface ISchemaCompareDialogService
{
    Task ShowDialogAsync(Window parentWindow);
}
