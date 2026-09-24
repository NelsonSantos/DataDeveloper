using System.Runtime.CompilerServices;
using ReactiveUI.Reactive.Builder;

namespace DataDeveloper.Tests;

internal static class ReactiveUITestInitializer
{
    // ReactiveUI 24 requires explicit initialization. The app does it via UseReactiveUI in
    // Program.BuildAvaloniaApp; tests construct view models directly, so initialize once per assembly.
    [ModuleInitializer]
    internal static void Initialize()
    {
        RxAppBuilder.CreateReactiveUIBuilder()
            .WithCoreServices()
            .BuildApp();
    }
}
