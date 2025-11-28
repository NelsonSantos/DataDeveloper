using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using DynamicData;

public class SqlEditorWithCompletion
{
    private readonly TextEditor _editor;
    private readonly SqlSuggestionEngine _engine;
    private CompletionWindow? _completionWindow;

    public SqlEditorWithCompletion(TextEditor editor, SqlSuggestionEngine engine)
    {
        _editor = editor;
        _engine = engine;

        _editor.Text = string.Empty;
        
        _editor.TextArea.TextEntered += OnTextEntered;
        //_editor.TextArea.TextEntering += OnTextEntering;
        _editor.TextArea.KeyDown += TextAreaOnKeyDown;
    }

    private void TextAreaOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.OemPeriod)
        {
            
        }
    }

    private string GetCurrentWord()
    {
        var offset = _editor.CaretOffset;
        var text = _editor.Text;

        int start = offset - 1;
        while (start > 0 && char.IsLetterOrDigit(text[start]))
            start--;

        return text.Substring(start + 1, offset - start - 1);
    }
    
    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        int caret = _editor.CaretOffset;
        var context = SqlContextAnalyzer.Analyze(_editor.Text, caret);
        var suggestions = _engine.GetSuggestions(context).OrderBy(s => s.Text).ToList();
        
        if (suggestions.Any())
        {
            _completionWindow?.Close();
            _completionWindow = new CompletionWindow(_editor.TextArea);
            _completionWindow.CompletionList.FontFamily = Application.Current.Resources["MonospaceFont"] as FontFamily;
            _completionWindow.StartOffset = _editor.TextArea.Caret.Offset;

            var currentWord = GetCurrentWord();
            var filtered = suggestions
                .Where(s => s.Text.Contains(currentWord, StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            Console.WriteLine($"CARET: {caret}");
            
            var data = _completionWindow.CompletionList.CompletionData;

            data.AddRange(filtered);
        
            _completionWindow.Show();
        }
    }
}