using System.Reactive;
using Avalonia;
using DataDeveloper.Core;
using DataDeveloper.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.ViewModels;

public class ExportConnectionsOptionsViewModel : ViewModelBase
{
    public ExportConnectionsOptionsViewModel()
    {
        ExportCommand = ReactiveCommand.Create<StyledElement>(element => Close(element, true));
        CancelCommand = ReactiveCommand.Create<StyledElement>(element => Close(element, false));
    }

    [Reactive] public bool IncludePasswords { get; set; }

    public ReactiveCommand<StyledElement, Unit> ExportCommand { get; }
    public ReactiveCommand<StyledElement, Unit> CancelCommand { get; }

    private static void Close(StyledElement element, bool confirmed)
    {
        var window = element.GetParentWindow();
        window?.Close(confirmed);
    }
}
