using Avalonia.Controls;

namespace DataDeveloper.Interfaces;

public interface IWindowStateService
{
    void Save(Window window);
    void Restore(Window window);
}