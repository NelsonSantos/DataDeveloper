using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using DataDeveloper.Core;
using DataDeveloper.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.ViewModels;

public class GenerateGuidViewModel : ViewModelBase
{
    private const string SettingsFileName = "generate-guid-tool.json";
    private const string SettingsSubfolder = "Config";

    private static readonly (string Label, string Specifier)[] Formats =
    {
        ("8-4-4-4-12 (hyphenated)", "D"),
        ("No hyphens", "N"),
        ("Braces {8-4-4-4-12}", "B"),
        ("Parentheses (8-4-4-4-12)", "P")
    };

    private readonly AppDataFileService _fileService;
    private readonly GenerateGuidToolSettings _settings;
    private Window? _window;

    public GenerateGuidViewModel(AppDataFileService fileService)
    {
        _fileService = fileService;
        _settings = _fileService.LoadJson<GenerateGuidToolSettings>(SettingsFileName, SettingsSubfolder) ?? new GenerateGuidToolSettings();

        SelectedFormat = FormatOptions[0];

        RegenerateCommand = ReactiveCommand.Create(Regenerate);
        PrimaryActionCommand = ReactiveCommand.CreateFromTask(ExecutePrimaryActionAsync);

        // The flyout items only change which mode is selected — they never copy or close
        // themselves. The action (copy, optionally close) runs on the next click of the
        // primary button, using whichever mode is selected at that time.
        SelectCopyCommand = ReactiveCommand.Create(() => SelectMode(closeAfter: false));
        SelectCopyAndCloseCommand = ReactiveCommand.Create(() => SelectMode(closeAfter: true));

        UpdateActionState();

        this.WhenAnyValue(vm => vm.SelectedFormat, vm => vm.Quantity, vm => vm.Uppercase)
            .Subscribe(_ => Regenerate());
        this.WhenAnyValue(vm => vm.Quantity)
            .Subscribe(_ => UpdateActionState());
    }

    public string[] FormatOptions { get; } = Formats.Select(f => f.Label).ToArray();

    [Reactive] public string SelectedFormat { get; set; } = string.Empty;
    [Reactive] public int Quantity { get; set; } = 1;
    [Reactive] public bool Uppercase { get; set; }
    [Reactive] public string GeneratedText { get; private set; } = string.Empty;
    [Reactive] public string PrimaryActionLabel { get; private set; } = "Copy";
    [Reactive] public string CopyLabel { get; private set; } = "Copy";
    [Reactive] public string CopyAndCloseLabel { get; private set; } = "Copy and close";

    public ReactiveCommand<Unit, Unit> RegenerateCommand { get; }
    public ReactiveCommand<Unit, Unit> PrimaryActionCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectCopyCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectCopyAndCloseCommand { get; }

    public void AttachWindow(Window window)
    {
        _window = window;
    }

    private void Regenerate()
    {
        var quantity = Math.Clamp(Quantity, 1, 100);
        var specifier = Formats.FirstOrDefault(f => f.Label == SelectedFormat).Specifier ?? "D";

        var lines = Enumerable.Range(0, quantity)
            .Select(_ => Guid.NewGuid().ToString(specifier))
            .Select(guid => Uppercase ? guid.ToUpperInvariant() : guid);

        GeneratedText = string.Join(Environment.NewLine, lines);
    }

    private void SelectMode(bool closeAfter)
    {
        _settings.PreferCopyAndClose = closeAfter;
        UpdateActionState();
        _fileService.SaveJson(SettingsFileName, _settings, SettingsSubfolder);
    }

    private async Task ExecutePrimaryActionAsync()
    {
        var shouldClose = _settings.PreferCopyAndClose;
        await CopyToClipboardAsync();
        if (shouldClose)
            CloseWindow();
    }

    private void UpdateActionState()
    {
        var isSingular = Math.Clamp(Quantity, 1, 100) == 1;
        CopyLabel = isSingular ? "Copy" : "Copy all";
        CopyAndCloseLabel = isSingular ? "Copy and close" : "Copy all and close";
        PrimaryActionLabel = _settings.PreferCopyAndClose ? CopyAndCloseLabel : CopyLabel;
    }

    private async Task CopyToClipboardAsync()
    {
        var topLevel = _window is null ? null : TopLevel.GetTopLevel(_window);
        if (topLevel?.Clipboard is null)
            return;

        await topLevel.Clipboard.SetTextAsync(GeneratedText);
    }

    private void CloseWindow()
    {
        // Deferred so the close doesn't race with the SplitButton's own popup teardown.
        Dispatcher.UIThread.Post(() => _window?.Close());
    }
}
