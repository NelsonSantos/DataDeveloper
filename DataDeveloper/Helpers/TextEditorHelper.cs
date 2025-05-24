using Avalonia;
using AvaloniaEdit;
using System;

namespace DataDeveloper.Helpers;

public static class TextEditorHelper
{
    public static readonly AttachedProperty<string> BindableTextProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, string>(
            "BindableText",
            typeof(TextEditorHelper),
            default!,
            inherits: false,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay,
            coerce: OnBindableTextChanged);

    public static string GetBindableText(TextEditor editor) =>
        editor.GetValue(BindableTextProperty);

    public static void SetBindableText(TextEditor editor, string value) =>
        editor.SetValue(BindableTextProperty, value);

    private static string OnBindableTextChanged(AvaloniaObject avaloniaObject, string newValue)
    {
        var editor = (TextEditor)avaloniaObject;
        // Evita loop de evento
        if (editor.Text != newValue)
        {
            editor.TextChanged -= OnTextChanged;
            editor.Text = newValue ?? string.Empty;
            editor.TextChanged += OnTextChanged;
        }

        return newValue ?? string.Empty;
    }

    private static void OnTextChanged(object? sender, System.EventArgs e)
    {
        if (sender is TextEditor editor)
        {
            SetBindableText(editor, editor.Text);
        }
    }
}