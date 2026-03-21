using Avalonia;
using Avalonia.Controls;
using DataDeveloper.Core;
using DataDeveloper.Interfaces;
using DataDeveloper.Models;

namespace DataDeveloper.Services;

public class WindowStateService : IWindowStateService
{
    private const string FileName = "window-state.json";
    private readonly AppDataFileService _fileService;
    private const string Subfolder = "Config";

    public WindowStateService(AppDataFileService fileService)
    {
        _fileService = fileService;
    }

    public void Save(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
            return;

        var state = new WindowStateInfo
        {
            Width = window.Width,
            Height = window.Height,
            X = window.Position.X,
            Y = window.Position.Y,
            WindowState = window.WindowState
        };

        _fileService.SaveJson(FileName, state, Subfolder);
    }

    public void Restore(Window window)
    {
        var state = _fileService.LoadJson<WindowStateInfo>(FileName, Subfolder);

        if (state is null)
            return;

        window.Width = state.Width;
        window.Height = state.Height;
        window.Position = new PixelPoint((int)state.X, (int)state.Y);

        // Apenas restaura se não for minimized
        if (state.WindowState != WindowState.Minimized)
            window.WindowState = state.WindowState;
    }
}