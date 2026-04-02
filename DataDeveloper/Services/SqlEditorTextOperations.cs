using System;
using System.Collections.Generic;
using System.Text;
using DataDeveloper.Data.Enums;

namespace DataDeveloper.Services;

public static class SqlEditorTextOperations
{
    public static TextEditResult? ToUpper(string text, int selectionStart, int selectionLength)
    {
        return TransformSelection(text, selectionStart, selectionLength, value => value.ToUpperInvariant());
    }

    public static TextEditResult? ToLower(string text, int selectionStart, int selectionLength)
    {
        return TransformSelection(text, selectionStart, selectionLength, value => value.ToLowerInvariant());
    }

    public static TextEditResult Indent(string text, int selectionStart, int selectionLength, string indentation)
    {
        var lineRange = GetLineRange(text, selectionStart, selectionLength);
        var segment = text.Substring(lineRange.Start, lineRange.Length);
        var lines = SplitLines(segment);
        var builder = new StringBuilder(segment.Length + lines.Count * indentation.Length);

        foreach (var line in lines)
        {
            if (line.Content.Length == 0 && line.LineEnding.Length == 0)
                continue;

            builder.Append(indentation);
            builder.Append(line.Content);
            builder.Append(line.LineEnding);
        }

        if (selectionLength == 0)
        {
            return new TextEditResult(
                lineRange.Start,
                lineRange.Length,
                builder.ToString(),
                selectionStart + indentation.Length,
                0);
        }

        return new TextEditResult(
            lineRange.Start,
            lineRange.Length,
            builder.ToString(),
            lineRange.Start,
            builder.Length);
    }

    public static TextEditResult Unindent(string text, int selectionStart, int selectionLength, string indentation)
    {
        var lineRange = GetLineRange(text, selectionStart, selectionLength);
        var segment = text.Substring(lineRange.Start, lineRange.Length);
        var lines = SplitLines(segment);
        var builder = new StringBuilder(segment.Length);
        var currentLineRemoved = 0;
        var currentLineIndex = GetCurrentLineIndex(lines, selectionStart - lineRange.Start);

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var removed = GetUnindentLength(line.Content, indentation);
            if (i == currentLineIndex)
                currentLineRemoved = removed;

            builder.Append(line.Content.AsSpan(removed));
            builder.Append(line.LineEnding);
        }

        if (selectionLength == 0)
        {
            return new TextEditResult(
                lineRange.Start,
                lineRange.Length,
                builder.ToString(),
                Math.Max(lineRange.Start, selectionStart - currentLineRemoved),
                0);
        }

        return new TextEditResult(
            lineRange.Start,
            lineRange.Length,
            builder.ToString(),
            lineRange.Start,
            builder.Length);
    }

    public static TextEditResult Comment(string text, int selectionStart, int selectionLength)
    {
        var lineRange = GetLineRange(text, selectionStart, selectionLength);
        var segment = text.Substring(lineRange.Start, lineRange.Length);
        var lines = SplitLines(segment);
        var builder = new StringBuilder(segment.Length + lines.Count * 3);
        var currentLineDelta = 0;
        var currentLineIndex = GetCurrentLineIndex(lines, selectionStart - lineRange.Start);

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var indentLength = GetLeadingWhitespaceLength(line.Content);
            builder.Append(line.Content.AsSpan(0, indentLength));
            builder.Append("-- ");
            builder.Append(line.Content.AsSpan(indentLength));
            builder.Append(line.LineEnding);

            if (i == currentLineIndex)
                currentLineDelta = 3;
        }

        if (selectionLength == 0)
        {
            return new TextEditResult(
                lineRange.Start,
                lineRange.Length,
                builder.ToString(),
                selectionStart + currentLineDelta,
                0);
        }

        return new TextEditResult(
            lineRange.Start,
            lineRange.Length,
            builder.ToString(),
            lineRange.Start,
            builder.Length);
    }

    public static TextEditResult Uncomment(string text, int selectionStart, int selectionLength)
    {
        var lineRange = GetLineRange(text, selectionStart, selectionLength);
        var segment = text.Substring(lineRange.Start, lineRange.Length);
        var lines = SplitLines(segment);
        var builder = new StringBuilder(segment.Length);
        var currentLineRemoved = 0;
        var currentLineIndex = GetCurrentLineIndex(lines, selectionStart - lineRange.Start);

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var removed = GetCommentPrefixLength(line.Content);
            if (i == currentLineIndex)
                currentLineRemoved = removed;

            if (removed > 0)
            {
                var indentLength = GetLeadingWhitespaceLength(line.Content);
                builder.Append(line.Content.AsSpan(0, indentLength));
                builder.Append(line.Content.AsSpan(indentLength + removed));
            }
            else
            {
                builder.Append(line.Content);
            }

            builder.Append(line.LineEnding);
        }

        if (selectionLength == 0)
        {
            return new TextEditResult(
                lineRange.Start,
                lineRange.Length,
                builder.ToString(),
                Math.Max(lineRange.Start, selectionStart - currentLineRemoved),
                0);
        }

        return new TextEditResult(
            lineRange.Start,
            lineRange.Length,
            builder.ToString(),
            lineRange.Start,
            builder.Length);
    }

    public static TextEditResult Beautify(string text, int selectionStart, int selectionLength, string indentation, DatabaseType databaseType)
    {
        var rangeStart = selectionLength > 0 ? selectionStart : 0;
        var rangeLength = selectionLength > 0 ? selectionLength : text.Length;
        var formatted = SqlTokenFormatter.Format(text.Substring(rangeStart, rangeLength), databaseType, indentation);
        var selectionStartAfter = selectionLength > 0 ? rangeStart : Math.Min(selectionStart, formatted.Length);
        var selectionLengthAfter = selectionLength > 0 ? formatted.Length : 0;

        return new TextEditResult(rangeStart, rangeLength, formatted, selectionStartAfter, selectionLengthAfter);
    }

    private static TextEditResult? TransformSelection(string text, int selectionStart, int selectionLength, Func<string, string> transform)
    {
        if (selectionLength <= 0)
            return null;

        var replacement = transform(text.Substring(selectionStart, selectionLength));
        return new TextEditResult(selectionStart, selectionLength, replacement, selectionStart, replacement.Length);
    }

    private static LineRange GetLineRange(string text, int selectionStart, int selectionLength)
    {
        var start = FindLineStart(text, selectionStart);
        var end = selectionLength == 0
            ? FindLineEnd(text, selectionStart)
            : FindLineEnd(text, selectionStart + selectionLength);

        return new LineRange(start, end - start);
    }

    private static int FindLineStart(string text, int offset)
    {
        var index = Math.Clamp(offset, 0, text.Length);
        while (index > 0 && text[index - 1] is not '\n' and not '\r')
            index--;

        return index;
    }

    private static int FindLineEnd(string text, int offset)
    {
        var index = Math.Clamp(offset, 0, text.Length);
        while (index < text.Length && text[index] is not '\n' and not '\r')
            index++;

        if (index < text.Length)
        {
            if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                return index + 2;

            return index + 1;
        }

        return index;
    }

    private static List<LineSegment> SplitLines(string value)
    {
        var lines = new List<LineSegment>();
        var index = 0;

        while (index < value.Length)
        {
            var lineStart = index;
            while (index < value.Length && value[index] is not '\n' and not '\r')
                index++;

            var content = value.Substring(lineStart, index - lineStart);
            var lineEnding = string.Empty;

            if (index < value.Length)
            {
                if (value[index] == '\r' && index + 1 < value.Length && value[index + 1] == '\n')
                {
                    lineEnding = "\r\n";
                    index += 2;
                }
                else
                {
                    lineEnding = value[index].ToString();
                    index++;
                }
            }

            lines.Add(new LineSegment(content, lineEnding));
        }

        if (value.Length == 0)
            lines.Add(new LineSegment(string.Empty, string.Empty));

        return lines;
    }

    private static int GetLeadingWhitespaceLength(string value)
    {
        var index = 0;
        while (index < value.Length && char.IsWhiteSpace(value[index]))
            index++;

        return index;
    }

    private static int GetUnindentLength(string line, string indentation)
    {
        if (line.StartsWith(indentation, StringComparison.Ordinal))
            return indentation.Length;

        var spaces = 0;
        while (spaces < line.Length && spaces < indentation.Length && line[spaces] == ' ')
            spaces++;

        return spaces;
    }

    private static int GetCommentPrefixLength(string line)
    {
        var indentLength = GetLeadingWhitespaceLength(line);
        if (line.Length <= indentLength + 1 || line[indentLength] != '-' || line[indentLength + 1] != '-')
            return 0;

        return line.Length > indentLength + 2 && line[indentLength + 2] == ' ' ? 3 : 2;
    }

    private static int GetCurrentLineIndex(IReadOnlyList<LineSegment> lines, int relativeOffset)
    {
        var offset = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            var lineLength = lines[i].Content.Length + lines[i].LineEnding.Length;
            if (relativeOffset <= offset + lineLength)
                return i;

            offset += lineLength;
        }

        return Math.Max(0, lines.Count - 1);
    }

    private readonly record struct LineRange(int Start, int Length);
    private readonly record struct LineSegment(string Content, string LineEnding);
}

public readonly record struct TextEditResult(
    int ReplaceStart,
    int ReplaceLength,
    string ReplacementText,
    int SelectionStart,
    int SelectionLength);
