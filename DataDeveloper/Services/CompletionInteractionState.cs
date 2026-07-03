namespace DataDeveloper.Services;

internal sealed class CompletionInteractionState
{
    private bool _hasActiveAutoCompletion;
    private bool _reopenCompletionOnWhitespace;

    // Only the literal space bar arms/reopens completion: char.IsWhiteSpace also
    // matches '\n'/'\r'/'\t', which made pressing Enter (or auto-indent) reopen completion.
    private static bool IsTriggerSpace(char ch) => ch == ' ';

    public void RememberAutoCompletion()
    {
        _hasActiveAutoCompletion = true;
    }

    public bool HandleTextEntered(string? text)
    {
        if (string.IsNullOrEmpty(text) || !IsTriggerSpace(text[0]))
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

        if (IsTriggerSpace(text[0]))
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
