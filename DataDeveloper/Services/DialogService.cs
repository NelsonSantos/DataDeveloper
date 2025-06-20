using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
            ContentTitle = title,
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
        };
    }

    public async Task ShowMessageAsync(string message, string? title = null)
    {
        await MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
        {
            ContentTitle = title,
            ContentMessage = message,
            ButtonDefinitions = ButtonEnum.Ok,
            Icon = Icon.Info,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        }).ShowAsPopupAsync(GetOwnerWindow());
    }

    public Task<string?> ShowSaveFileDialogAsync(string? suggestedName = null, string? title = null)
    {
        var dialog = new SaveFileDialog
        {
            Title = title ?? "Save file...",
            InitialFileName = suggestedName ?? "resultado.sql",
            DefaultExtension = "sql",
            ShowOverwritePrompt = true,
            Filters = new List<FileDialogFilter>
            {
                new FileDialogFilter { Name = "Sql file", Extensions = { "sql" } },
                new FileDialogFilter { Name = "All files", Extensions = { "*" } }
            }
        };

        return dialog.ShowAsync(GetOwnerWindow());
    }
    public async Task<string?> ShowOpenFileAsync(string? title = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = title ?? "Open file...",
            AllowMultiple = false,
            Filters = new List<FileDialogFilter>
            {
                new FileDialogFilter { Name = "Sql file", Extensions = { "sql" } },
                new FileDialogFilter { Name = "All files", Extensions = { "*" } }
            }
        };

        var result = await dialog.ShowAsync(GetOwnerWindow());
        return result?.FirstOrDefault();
    }    
}