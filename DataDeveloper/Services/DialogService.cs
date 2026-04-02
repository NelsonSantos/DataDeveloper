using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using DataDeveloper.Enums;
using DataDeveloper.Interfaces;
using DataDeveloper.ViewModels;
using DataDeveloper.Views;

namespace DataDeveloper.Services;

public class DialogService : IDialogService
{
    public async Task<DialogResult> ShowDialogAsync(string message, string? title = null, DialogButtons buttons = DialogButtons.Ok, DialogIcon icon = DialogIcon.Info)
    {
        var owner = GetOwnerWindow();
        var dialog = new AppDialogWindow(AppDialogViewModel.Create(message, title, buttons, icon));

        return await dialog.ShowDialog<DialogResult>(owner);
    }

    private Window GetOwnerWindow()
    {
        var window = Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => desktop.Windows.FirstOrDefault(w => w.IsActive)
                ?? desktop.Windows.FirstOrDefault(w => w.IsVisible)
                ?? desktop.MainWindow,
            _ => null
        };
        return window ?? throw new Exception("Failed to capture owner window.");
    }

    public async Task<DialogResult> ShowDialogResult(string message, string? title = null)
    {
        return await ShowDialogAsync(message, title, DialogButtons.YesNoCancel, DialogIcon.Question);
    }

    public async Task ShowMessageAsync(string message, string? title = null)
    {
        _ = await ShowDialogAsync(message, title, DialogButtons.Ok, DialogIcon.Info);
    }

    public async Task<string?> ShowSaveFileDialogAsync(string? suggestedName = null, string? title = null)
    {
        var owner = GetOwnerWindow();
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title ?? "Save file...",
            SuggestedFileName = suggestedName ?? "resultado.sql",
            DefaultExtension = "sql",
            ShowOverwritePrompt = true,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("Sql file")
                {
                    Patterns = new[] { "*.sql" },
                    AppleUniformTypeIdentifiers = new[] { "public.sql" },
                    MimeTypes = new[] { "application/sql", "text/plain" }
                },
                FilePickerFileTypes.All
            }
        });

        return file?.TryGetLocalPath();
    }
    public async Task<string?> ShowOpenFileAsync(string? title = null)
    {
        var owner = GetOwnerWindow();
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title ?? "Open file...",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Sql file")
                {
                    Patterns = new[] { "*.sql" },
                    AppleUniformTypeIdentifiers = new[] { "public.sql" },
                    MimeTypes = new[] { "application/sql", "text/plain" }
                },
                FilePickerFileTypes.All
            }
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> ShowOpenDatabaseFileAsync(string? title = null)
    {
        var owner = GetOwnerWindow();
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title ?? "Select database file...",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("SQLite database")
                {
                    Patterns = new[] { "*.db", "*.sqlite", "*.sqlite3" },
                    AppleUniformTypeIdentifiers = new[] { "public.database" },
                    MimeTypes = new[] { "application/vnd.sqlite3", "application/octet-stream" }
                },
                FilePickerFileTypes.All
            }
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> ShowCreateDatabaseFileAsync(string? suggestedName = null, string? title = null)
    {
        var owner = GetOwnerWindow();
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title ?? "Create database file...",
            SuggestedFileName = suggestedName ?? "database.db",
            DefaultExtension = "db",
            ShowOverwritePrompt = true,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("SQLite database")
                {
                    Patterns = new[] { "*.db", "*.sqlite", "*.sqlite3" },
                    AppleUniformTypeIdentifiers = new[] { "public.database" },
                    MimeTypes = new[] { "application/vnd.sqlite3", "application/octet-stream" }
                },
                FilePickerFileTypes.All
            }
        });

        return file?.TryGetLocalPath();
    }
}
