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
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;

namespace DataDeveloper.Services;

public class DialogService : IDialogService
{
    private Window GetOwnerWindow()
    {
        var window = Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => desktop.Windows.FirstOrDefault(w => w.IsActive),
            _ => null
        };
        return window ?? throw new Exception("Failed to capture owner window.");
    }

    public async Task<DialogResult> ShowDialogResult(string message, string? title = null)
    {
        var messageBox = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
        {
            ContentTitle = title ?? string.Empty,
            ContentMessage = message,
            ButtonDefinitions = ButtonEnum.YesNoCancel,
            Icon = Icon.Question,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        });
        var result = await messageBox.ShowAsPopupAsync(GetOwnerWindow());

        return result switch
        {
            ButtonResult.Yes => DialogResult.Yes,
            ButtonResult.No => DialogResult.No,
            ButtonResult.Cancel => DialogResult.Cancel,
            _ => DialogResult.Cancel
        };
    }

    public async Task ShowMessageAsync(string message, string? title = null)
    {
        await MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
        {
            ContentTitle = title ?? string.Empty,
            ContentMessage = message,
            ButtonDefinitions = ButtonEnum.Ok,
            Icon = Icon.Info,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        }).ShowAsPopupAsync(GetOwnerWindow());
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
}
