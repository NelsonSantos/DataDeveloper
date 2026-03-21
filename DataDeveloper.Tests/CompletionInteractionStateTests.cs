using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class CompletionInteractionStateTests
{
    [Fact]
    public void HandleTextEntered_WhitespaceWithoutArmedReopen_ReturnsNull()
    {
        var state = new CompletionInteractionState();

        var request = state.HandleTextEntered(" ");

        Assert.Null(request);
    }

    [Fact]
    public void HandleTextEntered_WhitespaceAfterWindowWhitespaceInput_ReopensLastAutoRequest()
    {
        var state = new CompletionInteractionState();
        var request = SqlCompletionProvider.GetManualCompletionRequest("select ", "select ".Length);

        state.RememberAutoCompletion(request);
        state.ShouldRequestInsertion(" ", hasCompletionWindow: true);

        var reopened = state.HandleTextEntered(" ");

        Assert.Same(request, reopened);
    }

    [Fact]
    public void ShouldRequestInsertion_WhitespaceWithOpenWindow_DoesNotInsertAndArmsReopen()
    {
        var state = new CompletionInteractionState();
        var request = SqlCompletionProvider.GetManualCompletionRequest("select ", "select ".Length);
        state.RememberAutoCompletion(request);

        var shouldInsert = state.ShouldRequestInsertion(" ", hasCompletionWindow: true);
        var reopened = state.HandleTextEntered(" ");

        Assert.False(shouldInsert);
        Assert.Same(request, reopened);
    }

    [Fact]
    public void ShouldRequestInsertion_CommaWithOpenWindow_RequestsInsertion()
    {
        var state = new CompletionInteractionState();

        var shouldInsert = state.ShouldRequestInsertion(",", hasCompletionWindow: true);

        Assert.True(shouldInsert);
    }

    [Fact]
    public void ShouldRequestInsertion_LetterWithOpenWindow_DoesNotRequestInsertion()
    {
        var state = new CompletionInteractionState();

        var shouldInsert = state.ShouldRequestInsertion("a", hasCompletionWindow: true);

        Assert.False(shouldInsert);
    }

    [Fact]
    public void ResetWhitespaceReopen_ClearsPendingReopen()
    {
        var state = new CompletionInteractionState();
        var request = SqlCompletionProvider.GetManualCompletionRequest("select ", "select ".Length);
        state.RememberAutoCompletion(request);
        state.ShouldRequestInsertion(" ", hasCompletionWindow: true);
        state.ResetWhitespaceReopen();

        var reopened = state.HandleTextEntered(" ");

        Assert.Null(reopened);
    }
}
