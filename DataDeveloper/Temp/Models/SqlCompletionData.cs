using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using System;
using Avalonia.Media;

public class SqlCompletionData : ICompletionData
{
    public SqlCompletionData(string text, string description)
    {
        Text = text;
        Description = description;
    }
    public IImage? Image => null;
    public string Text { get; }

    public object Content => Text;

    public object? Description { get; }

    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        // Substitui a palavra parcialmente digitada antes do cursor
        var offset = textArea.Caret.Offset;
        var document = textArea.Document;

        // Encontra o início da palavra atual
        int startOffset = offset;
        while (startOffset > 0 && (char.IsLetterOrDigit(document.GetCharAt(startOffset - 1)) || document.GetCharAt(startOffset - 1) == '_'))
            startOffset--;

        int length = offset - startOffset;

        // Substitui a palavra parcial pelo texto da sugestão
        document.Replace(startOffset, length, Text);        
    }
}
