namespace DataDeveloper.Services;

internal sealed class CompletionInteractionState
{
    private bool _hasActiveAutoCompletion;
    private bool _reopenCompletionOnWhitespace;

    public void RememberAutoCompletion()
    {
        _hasActiveAutoCompletion = true;
    }

    public bool HandleTextEntered(string? text)
    {
        if (string.IsNullOrEmpty(text) || !char.IsWhiteSpace(text[0]))
        {
            _reopenCompletionOnWhitespace = false;
            return false;
        }

        if (!_reopenCompletionOnWhitespace)
            return false;

        _reopenCompletionOnWhitespace = false;
        return true;
    }

    public bool ShouldRequestInsertion(string? text, bool hasCompletionWindow)
    {
        if (!hasCompletionWindow || string.IsNullOrEmpty(text))
            return false;

        if (char.IsWhiteSpace(text[0]))
        {
            _reopenCompletionOnWhitespace = _hasActiveAutoCompletion;
            return false;
        }

        _reopenCompletionOnWhitespace = false;
        return !char.IsLetterOrDigit(text[0]) && text[0] is not '_' and not '.';
    }

    public void ResetWhitespaceReopen()
    {
        _reopenCompletionOnWhitespace = false;
        _hasActiveAutoCompletion = false;
    }
}
