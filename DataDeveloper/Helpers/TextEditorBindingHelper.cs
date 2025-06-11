using Avalonia;
using AvaloniaEdit;
using Avalonia.Threading;
using System;
using System.Runtime.CompilerServices;
using Avalonia.Data;

public static class TextEditorBindingHelper
{
    public static readonly AttachedProperty<string> BindableTextProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, string>(
            "BindableText",
            typeof(TextEditorBindingHelper),
            default!,
            inherits: false,
            defaultBindingMode: BindingMode.TwoWay,
            coerce: OnBindableTextChanged);

    public static readonly AttachedProperty<string> BindableSelectedTextProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, string>(
            "BindableSelectedText",
            typeof(TextEditorBindingHelper),
            default!,
            defaultBindingMode: BindingMode.OneWayToSource);
    
    public static readonly AttachedProperty<int> BindableCaretOffsetProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, int>(
            "BindableCaretOffset",
            typeof(TextEditorBindingHelper),
            default!,
            defaultBindingMode: BindingMode.OneWayToSource);

    public static readonly AttachedProperty<int> BindableCaretLineProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, int>(
            "BindableCaretLine",
            typeof(TextEditorBindingHelper),
            default!,
            defaultBindingMode: BindingMode.OneWayToSource);
    
    public static readonly AttachedProperty<int> BindableCaretColumnProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, int>(
            "BindableCaretColumn",
            typeof(TextEditorBindingHelper),
            default!,
            defaultBindingMode: BindingMode.OneWayToSource);
    
    public static string GetBindableText(TextEditor editor) =>
        editor.GetValue(BindableTextProperty);

    public static void SetBindableText(TextEditor editor, string value) =>
        editor.SetValue(BindableTextProperty, value);
    
    public static string GetBindableSelectedText(TextEditor editor) =>
        editor.GetValue(BindableSelectedTextProperty);

    public static void SetBindableSelectedText(TextEditor editor, string value) =>
        editor.SetValue(BindableSelectedTextProperty, value);

    public static int GetBindableCaretOffset(TextEditor editor) =>
        editor.GetValue(BindableCaretOffsetProperty);

    public static void SetBindableCaretOffset(TextEditor editor, int value) =>
        editor.SetValue(BindableCaretOffsetProperty, value);

    public static int GetBindableCaretLine(TextEditor editor) =>
        editor.GetValue(BindableCaretLineProperty);

    public static void SetBindableCaretLine(TextEditor editor, int value) =>
        editor.SetValue(BindableCaretLineProperty, value);
    
    public static int GetBindableCaretColumn(TextEditor editor) =>
        editor.GetValue(BindableCaretColumnProperty);

    public static void SetBindableCaretColumn(TextEditor editor, int value) =>
        editor.SetValue(BindableCaretColumnProperty, value);
    
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(150);

    private class EditorState
    {
        public DispatcherTimer? TextDebounce { get; set; }
        public DispatcherTimer? SelectionDebounce { get; set; }
        public DispatcherTimer? CaretDebounce { get; set; }
    }

    private static readonly ConditionalWeakTable<TextEditor, EditorState> _states = new();

    static TextEditorBindingHelper()
    {
        BindableTextProperty.Changed.Subscribe(args =>
        {
            if (args.Sender is TextEditor editor)
                AttachEvents(editor);
        });

        BindableSelectedTextProperty.Changed.Subscribe(args =>
        {
            if (args.Sender is TextEditor editor)
                AttachEvents(editor);
        });

        BindableCaretOffsetProperty.Changed.Subscribe(args =>
        {
            if (args.Sender is TextEditor editor)
                AttachEvents(editor);
        });

        BindableCaretLineProperty.Changed.Subscribe(args =>
        {
            if (args.Sender is TextEditor editor)
                AttachEvents(editor);
        });

        BindableCaretColumnProperty.Changed.Subscribe(args =>
        {
            if (args.Sender is TextEditor editor)
                AttachEvents(editor);
        });
        
    }

    private static void AttachEvents(TextEditor editor)
    {
        if (!_states.TryGetValue(editor, out _))
        {
            var state = new EditorState();
            _states.Add(editor, state);
            editor.TextChanged += (_, _) => DebouncedTextUpdate(editor);
            editor.TextArea.SelectionChanged += (_, _) => DebouncedSelectionUpdate(editor);
            editor.TextArea.Caret.PositionChanged += (_, _) => DebouncedCaretUpdate(editor);
        }
    }

    private static string OnBindableTextChanged(AvaloniaObject obj, string newValue)
    {
        if (obj is not TextEditor editor)
            return newValue;

        if (editor.Text != newValue)
        {
            editor.TextChanged -= OnDirectTextChanged;
            editor.Text = newValue ?? string.Empty;
            editor.TextChanged += OnDirectTextChanged;
        }

        return newValue ?? string.Empty;
    }

    private static void OnDirectTextChanged(object? sender, EventArgs e)
    {
        if (sender is TextEditor editor)
        {
            SetBindableText(editor, editor.Text);
        }
    }

    private static void DebouncedTextUpdate(TextEditor editor)
    {
        var state = _states.GetOrCreateValue(editor);
        state.TextDebounce?.Stop();
        state.TextDebounce = new DispatcherTimer { Interval = DebounceDelay };
        state.TextDebounce.Tick += (_, _) =>
        {
            state.TextDebounce?.Stop();
            SetBindableText(editor, editor.Text ?? "");
        };
        state.TextDebounce.Start();
    }

    private static void DebouncedSelectionUpdate(TextEditor editor)
    {
        var state = _states.GetOrCreateValue(editor);
        state.SelectionDebounce?.Stop();
        state.SelectionDebounce = new DispatcherTimer { Interval = DebounceDelay };
        state.SelectionDebounce.Tick += (_, _) =>
        {
            state.SelectionDebounce?.Stop();
            SetBindableSelectedText(editor, editor.SelectedText ?? "");
        };
        state.SelectionDebounce.Start();
    }

    private static void DebouncedCaretUpdate(TextEditor editor)
    {
        var state = _states.GetOrCreateValue(editor);
        state.CaretDebounce?.Stop();
        state.CaretDebounce = new DispatcherTimer { Interval = DebounceDelay };
        state.CaretDebounce.Tick += (_, _) =>
        {
            state.CaretDebounce?.Stop();
            var position = editor.TextArea.Caret.Position;
            SetBindableCaretOffset(editor, editor.CaretOffset);
            SetBindableCaretLine(editor, position.Line);
            SetBindableCaretColumn(editor, position.Column);
        };
        state.CaretDebounce.Start();
    }
}
