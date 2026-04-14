using System;
using System.Collections.Generic;

namespace DataDeveloper.Services;

public sealed record SqlFunctionCallContext(string FunctionName, int ArgumentIndex);

public static class SqlFunctionCallContextDetector
{
    public static SqlFunctionCallContext? Detect(string editorText, int caretOffset)
    {
        if (string.IsNullOrEmpty(editorText) || caretOffset <= 0)
            return null;

        var scanLength = Math.Min(caretOffset, editorText.Length);
        var stack = new List<FunctionFrame>();
        var state = ScanState.Normal;

        for (var index = 0; index < scanLength; index++)
        {
            var ch = editorText[index];
            var next = index + 1 < scanLength ? editorText[index + 1] : '\0';

            switch (state)
            {
                case ScanState.LineComment:
                    if (ch is '\r' or '\n')
                        state = ScanState.Normal;
                    continue;
                case ScanState.BlockComment:
                    if (ch == '*' && next == '/')
                    {
                        index++;
                        state = ScanState.Normal;
                    }
                    continue;
                case ScanState.SingleQuotedString:
                    if (ch == '\'' && next == '\'')
                    {
                        index++;
                        continue;
                    }
                    if (ch == '\'')
                        state = ScanState.Normal;
                    continue;
                case ScanState.DoubleQuotedIdentifier:
                    if (ch == '"' && next == '"')
                    {
                        index++;
                        continue;
                    }
                    if (ch == '"')
                        state = ScanState.Normal;
                    continue;
                case ScanState.BracketQuotedIdentifier:
                    if (ch == ']')
                        state = ScanState.Normal;
                    continue;
                case ScanState.BacktickQuotedIdentifier:
                    if (ch == '`')
                        state = ScanState.Normal;
                    continue;
            }

            if (ch == '-' && next == '-')
            {
                index++;
                state = ScanState.LineComment;
                continue;
            }

            if (ch == '/' && next == '*')
            {
                index++;
                state = ScanState.BlockComment;
                continue;
            }

            if (ch == '\'')
            {
                state = ScanState.SingleQuotedString;
                continue;
            }

            if (ch == '"')
            {
                state = ScanState.DoubleQuotedIdentifier;
                continue;
            }

            if (ch == '[')
            {
                state = ScanState.BracketQuotedIdentifier;
                continue;
            }

            if (ch == '`')
            {
                state = ScanState.BacktickQuotedIdentifier;
                continue;
            }

            if (ch == '(')
            {
                stack.Add(new FunctionFrame(ReadFunctionNameBeforeOpenParen(editorText, index), 0));
                continue;
            }

            if (ch == ')')
            {
                if (stack.Count > 0)
                    stack.RemoveAt(stack.Count - 1);
                continue;
            }

            if (ch == ',' && stack.Count > 0)
            {
                var topIndex = stack.Count - 1;
                stack[topIndex] = stack[topIndex] with { ArgumentIndex = stack[topIndex].ArgumentIndex + 1 };
            }
        }

        for (var index = stack.Count - 1; index >= 0; index--)
        {
            var frame = stack[index];
            if (!string.IsNullOrWhiteSpace(frame.FunctionName))
                return new SqlFunctionCallContext(frame.FunctionName, frame.ArgumentIndex);
        }

        return null;
    }

    private static string? ReadFunctionNameBeforeOpenParen(string text, int openParenIndex)
    {
        var index = openParenIndex - 1;
        while (index >= 0 && char.IsWhiteSpace(text[index]))
            index--;

        var end = index + 1;
        while (index >= 0 && IsIdentifierCharacter(text[index]))
            index--;

        if (end <= index + 1)
            return null;

        var functionName = text[(index + 1)..end];
        return string.IsNullOrWhiteSpace(functionName) ? null : functionName;
    }

    private static bool IsIdentifierCharacter(char ch)
    {
        return char.IsLetterOrDigit(ch) || ch is '_' or '$' or '#';
    }

    private readonly record struct FunctionFrame(string? FunctionName, int ArgumentIndex);

    private enum ScanState
    {
        Normal,
        LineComment,
        BlockComment,
        SingleQuotedString,
        DoubleQuotedIdentifier,
        BracketQuotedIdentifier,
        BacktickQuotedIdentifier
    }
}
