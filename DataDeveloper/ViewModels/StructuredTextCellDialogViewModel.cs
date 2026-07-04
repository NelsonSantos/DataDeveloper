using System;
using System.Reactive;
using Avalonia;
using DataDeveloper.Core;
using DataDeveloper.Helpers;
using DataDeveloper.NextGrid.Renderers;
using DataDeveloper.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.ViewModels;

public class StructuredTextCellDialogViewModel : ViewModelBase
{
    private string _initialText = string.Empty;

    public StructuredTextCellDialogViewModel()
    {
        this.WhenAnyValue(x => x.Text).Subscribe(text =>
        {
            // Sticky detection: once we know the value is JSON/XML, keep treating it as such
            // even while it's temporarily invalid mid-edit. Only switch Kind when the new text
            // clearly resolves to a (possibly different) structured format - never back to None.
            var detected = StructuredTextSniffer.Detect(text);
            if (detected != StructuredTextKind.None)
                Kind = detected;

            UpdateState();
        });

        PrettifyCommand = ReactiveCommand.Create(Prettify);
        MinifyCommand = ReactiveCommand.Create(Minify);
        OkCommand = ReactiveCommand.Create<StyledElement>(element => Close(element, saved: false));
        SaveCommand = ReactiveCommand.Create<StyledElement>(element => Close(element, saved: true));
        CancelCommand = ReactiveCommand.Create<StyledElement>(element => Close(element, saved: false));
    }

    [Reactive] public string Text { get; set; } = string.Empty;
    [Reactive] public StructuredTextKind Kind { get; set; }
    [Reactive] public bool IsEditable { get; set; }
    [Reactive] public bool IsDirty { get; set; }
    [Reactive] public bool HasInvalidTextWarning { get; set; }

    public bool ShowOk => IsEditable && !IsDirty;
    public bool ShowSaveCancel => IsEditable && IsDirty;
    public bool ShowClose => !IsEditable;
    public bool ShowFormattingButtons => Kind != StructuredTextKind.None;

    public string Title => Kind switch
    {
        StructuredTextKind.Xml => "XML",
        StructuredTextKind.Json => "JSON",
        _ => "Text"
    };

    public string InvalidWarningMessage =>
        $"This text is not valid {(Kind == StructuredTextKind.Xml ? "XML" : "JSON")}. You can still save it as-is.";

    public ReactiveCommand<Unit, Unit> PrettifyCommand { get; }
    public ReactiveCommand<Unit, Unit> MinifyCommand { get; }
    public ReactiveCommand<StyledElement, Unit> OkCommand { get; }
    public ReactiveCommand<StyledElement, Unit> SaveCommand { get; }
    public ReactiveCommand<StyledElement, Unit> CancelCommand { get; }

    public string CurrentText => Text;

    public void Initialize(string? value, bool isEditable, StructuredTextKind kind)
    {
        IsEditable = isEditable;
        Kind = kind;
        // Show the value exactly as stored - no auto-formatting. Prettify/Minify are explicit,
        // user-driven actions; only running them (or editing the text) should count as a change
        // worth saving.
        _initialText = value ?? string.Empty;
        Text = _initialText;
    }

    private void Prettify()
    {
        if (TryFormat(Text, indented: true, out var pretty))
            Text = pretty;
    }

    private void Minify()
    {
        if (TryFormat(Text, indented: false, out var compact))
            Text = compact;
    }

    private bool TryFormat(string? text, bool indented, out string result) => Kind switch
    {
        StructuredTextKind.Xml => XmlTextFormatter.TryFormat(text, indented, out result),
        StructuredTextKind.Json => JsonTextFormatter.TryFormat(text, indented, out result),
        _ => Fail(text, out result)
    };

    private bool IsValid(string? text) => Kind switch
    {
        StructuredTextKind.Xml => XmlTextFormatter.IsValidXml(text),
        StructuredTextKind.Json => JsonTextFormatter.IsValidJson(text),
        _ => true
    };

    private static bool Fail(string? text, out string result)
    {
        result = text ?? string.Empty;
        return false;
    }

    private void UpdateState()
    {
        IsDirty = Text != _initialText;
        HasInvalidTextWarning = !IsValid(Text);
        this.RaisePropertyChanged(nameof(ShowOk));
        this.RaisePropertyChanged(nameof(ShowSaveCancel));
        this.RaisePropertyChanged(nameof(ShowFormattingButtons));
        this.RaisePropertyChanged(nameof(Title));
    }

    private static void Close(StyledElement element, bool saved)
    {
        var window = element.GetParentWindow();
        window.Close(saved);
    }
}
