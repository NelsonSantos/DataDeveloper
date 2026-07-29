using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Interfaces;
using DataDeveloper.ViewModels;

namespace DataDeveloper.Services;

public class FileImportDialogService : IFileImportDialogService
{
    private readonly IConnectionDialogService _connectionDialogService;
    private readonly IDialogService _dialogService;
    private readonly ViewLocatorService _viewLocator;

    public FileImportDialogService(IConnectionDialogService connectionDialogService, IDialogService dialogService, ViewLocatorService viewLocator)
    {
        _connectionDialogService = connectionDialogService;
        _dialogService = dialogService;
        _viewLocator = viewLocator;
    }

    public async Task<IConnectionSettings?> ShowDialogAsync(Window parentWindow, IConnectionSettings? preselectedConnection = null)
    {
        // Built directly rather than resolved from DI: the wizard needs the caller's
        // preselected connection (if any) passed into its constructor, which a plain
        // GetRequiredService<FileImportViewModel>() call cannot provide.
        var model = new FileImportViewModel(_connectionDialogService, _dialogService, preselectedConnection);

        var dialog = _viewLocator.Build(model) as Window
                     ?? throw new InvalidOperationException("File import window could not be resolved.");

        await dialog.ShowDialog(parentWindow);

        return model.SelectedConnection;
    }
}
