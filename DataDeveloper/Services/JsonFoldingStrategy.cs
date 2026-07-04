using System.Collections.Generic;
using System.Linq;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace DataDeveloper.Services;

public sealed class JsonFoldingStrategy
{
    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        var newFoldings = CreateNewFoldings(document.Text).ToList();
        manager.UpdateFoldings(newFoldings, -1);
    }

    internal static IEnumerable<NewFolding> CreateNewFoldings(string text)
    {
        var foldings = new List<NewFolding>();
        var starts = new Stack<int>();
        var inString = false;
        var escapeNext = false;

        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];

            if (inString)
            {
                if (escapeNext)
                    escapeNext = false;
                else if (current == '\\')
                    escapeNext = true;
                else if (current == '"')
                    inString = false;

                continue;
            }

            switch (current)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                case '[':
                    starts.Push(index);
                    break;
                case '}':
                case ']':
                    if (starts.Count > 0)
                    {
                        var start = starts.Pop();
                        if (SpansMultipleLines(text, start, index))
                            foldings.Add(new NewFolding(start, index + 1));
                    }

                    break;
            }
        }

        foldings.Sort((left, right) => left.StartOffset.CompareTo(right.StartOffset));
        return foldings;
    }

    private static bool SpansMultipleLines(string text, int startOffset, int endOffset)
    {
        for (var index = startOffset; index < endOffset; index++)
        {
            if (text[index] == '\n')
                return true;
        }

        return false;
    }
}
