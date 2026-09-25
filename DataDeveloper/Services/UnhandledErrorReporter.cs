using System;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DataDeveloper.Core;
using DataDeveloper.Enums;
using DataDeveloper.Models;
using DataDeveloper.ViewModels;
using DataDeveloper.Views;

namespace DataDeveloper.Services;

/// <summary>
/// Last line of defense against crashes: errors nothing else handled (UI thread, unobserved tasks, ReactiveUI
/// commands) are written to <c>logs/errors.log</c> in the app data folder and shown to the user instead of closing
/// the app. A lost database connection (e.g. the VPN dropped) gets a message that says so.
/// </summary>
public static class UnhandledErrorReporter
{
    private const string LogFileName = "errors.log";
    private const string LogFolder = "logs";

    private static readonly AppDataFileService FileService = new();
    private static readonly object LogLock = new();
    private static bool _isDialogOpen;

    public static string LogFilePath => Path.Combine(AppDataFileService.AppDataDirectory, LogFolder, LogFileName);

    /// <summary>Hooks the process, task and UI-thread handlers. ReactiveUI's is passed to its builder.</summary>
    public static void Install()
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            e.Handled = true;
            Report(e.Exception, "UI");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            Log(e.Exception, "Task");
        };

        // The process is going down; at least leave a trace.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
                Log(exception, "Process");
        };
    }

    /// <summary>Logs the error and tells the user (one dialog at a time; others are only logged).</summary>
    public static void Report(Exception exception, string source)
    {
        if (IsCancellation(exception))
            return;

        Log(exception, source);
        Dispatcher.UIThread.Post(() => _ = ShowAsync(exception));
    }

    /// <summary>Only writes the error to the log (for failures the user does not need to act on).</summary>
    public static void Log(Exception exception, string source)
    {
        if (IsCancellation(exception))
            return;

        try
        {
            lock (LogLock)
                FileService.AppendLog(LogFileName, $"[{source}] {exception}", LogFolder);
        }
        catch
        {
            // Logging must never become the next crash.
        }
    }

    /// <summary>Whether the error means the database could not be reached (network, VPN, server down).</summary>
    public static bool IsConnectionFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException or TimeoutException or IOException or DbException { IsTransient: true })
                return true;

            if (current is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
                return IsConnectionFailure(aggregate.InnerExceptions[0]);
        }

        return false;
    }

    public static string BuildMessage(Exception exception)
    {
        var detail = (exception is AggregateException { InnerExceptions.Count: 1 } aggregate ? aggregate.InnerExceptions[0] : exception).Message;

        return IsConnectionFailure(exception)
            ? $"The database could not be reached. Check your network or VPN connection and try again.\n\n{detail}\n\nDetails were saved to {LogFilePath}"
            : $"Something went wrong, but Data Developer is still running.\n\n{detail}\n\nDetails were saved to {LogFilePath}";
    }

    private static async Task ShowAsync(Exception exception)
    {
        if (_isDialogOpen)
            return;

        _isDialogOpen = true;
        try
        {
            var title = IsConnectionFailure(exception) ? "Connection lost" : "Unexpected error";
            var dialog = new AppDialogWindow(AppDialogViewModel.Create(BuildMessage(exception), title, DialogButtons.Ok, DialogIcon.Error));
            if (FindOwnerWindow() is { } owner)
                await dialog.ShowDialog<DialogResult>(owner);
        }
        catch (Exception dialogException)
        {
            Log(dialogException, "ErrorDialog");
        }
        finally
        {
            _isDialogOpen = false;
        }
    }

    private static Window? FindOwnerWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows.FirstOrDefault(window => window.IsActive) ?? desktop.Windows.FirstOrDefault(window => window.IsVisible) ?? desktop.MainWindow
            : null;

    private static bool IsCancellation(Exception exception) =>
        exception is OperationCanceledException ||
        exception is AggregateException aggregate && aggregate.InnerExceptions.Count > 0 && aggregate.InnerExceptions.All(IsCancellation);
}
