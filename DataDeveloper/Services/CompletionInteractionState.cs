namespace DataDeveloper.Services;

internal sealed class CompletionInteractionState
{
    private CompletionRequest? _lastAutoCompletionRequest;
    private bool _reopenCompletionOnWhitespace;

    public void RememberAutoCompletion(CompletionRequest request)
    {
        _lastAutoCompletionRequest = request;
    }

    public CompletionRequest? HandleTextEntered(string? text)
    {
        if (string.IsNullOrEmpty(text) || !char.IsWhiteSpace(text[0]))
        {
            _reopenCompletionOnWhitespace = false;
            return null;
        }

        if (!_reopenCompletionOnWhitespace || _lastAutoCompletionRequest is null)
            return null;

        _reopenCompletionOnWhitespace = false;
        return _lastAutoCompletionRequest;
    }

    public bool ShouldRequestInsertion(string? text, bool hasCompletionWindow)
    {
        if (!hasCompletionWindow || string.IsNullOrEmpty(text))
            return false;

        if (char.IsWhiteSpace(text[0]))
        {
            _reopenCompletionOnWhitespace = true;
            return false;
        }

        _reopenCompletionOnWhitespace = false;
        return !char.IsLetterOrDigit(text[0]) && text[0] != '_';
    }

    public void ResetWhitespaceReopen()
    {
        _reopenCompletionOnWhitespace = false;
    }
}
