using System;
using System.Reactive;
using System.Threading;
using Avalonia;
using DataDeveloper.Services;
using ReactiveUI.Avalonia.Reactive;

namespace DataDeveloper;

public class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .With(new MacOSPlatformOptions
            {
                DisableDefaultApplicationMenuItems = true,
            })
            .UsePlatformDetect()
            .LogToTrace()
            // A failed command (e.g. the database became unreachable) is reported instead of crashing the app.
            .UseReactiveUI(builder => builder.WithExceptionHandler(
                Observer.Create<Exception>(exception => UnhandledErrorReporter.Report(exception, "ReactiveUI"))));
}
