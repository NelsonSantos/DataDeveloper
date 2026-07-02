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

        Assert.False(request);
    }

    [Fact]
    public void HandleTextEntered_WhitespaceAfterWindowWhitespaceInput_Reopens()
    {
        var state = new CompletionInteractionState();

        state.RememberAutoCompletion();
        state.ShouldRequestInsertion(" ", hasCompletionWindow: true);

        var reopened = state.HandleTextEntered(" ");

        Assert.True(reopened);
    }

    [Fact]
    public void ShouldRequestInsertion_WhitespaceWithOpenWindow_DoesNotInsertAndArmsReopen()
    {
        var state = new CompletionInteractionState();
        state.RememberAutoCompletion();

        var shouldInsert = state.ShouldRequestInsertion(" ", hasCompletionWindow: true);
        var reopened = state.HandleTextEntered(" ");

        Assert.False(shouldInsert);
        Assert.True(reopened);
    }

    [Fact]
    public void ShouldRequestInsertion_CommaWithOpenWindow_RequestsInsertion()
    {
        var state = new CompletionInteractionState();

        var shouldInsert = state.ShouldRequestInsertion(",", hasCompletionWindow: true);

        Assert.True(shouldInsert);
    }

    [Fact]
    public void ShouldRequestInsertion_DotWithOpenWindow_DoesNotRequestInsertion()
    {
        var state = new CompletionInteractionState();

        var shouldInsert = state.ShouldRequestInsertion(".", hasCompletionWindow: true);

        Assert.False(shouldInsert);
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
        state.RememberAutoCompletion();
        state.ShouldRequestInsertion(" ", hasCompletionWindow: true);
        state.ResetWhitespaceReopen();

        var reopened = state.HandleTextEntered(" ");

        Assert.False(reopened);
    }

    [Fact]
    public void ShouldRequestInsertion_WhitespaceWithManualWindow_DoesNotArmReopen()
    {
        var state = new CompletionInteractionState();

        var shouldInsert = state.ShouldRequestInsertion(" ", hasCompletionWindow: true);
        var reopened = state.HandleTextEntered(" ");

        Assert.False(shouldInsert);
        Assert.False(reopened);
    }

    [Fact]
    public void HandleTextEntered_Newline_NeverReopensEvenWhenArmed()
    {
        var state = new CompletionInteractionState();
        state.RememberAutoCompletion();
        state.ShouldRequestInsertion(" ", hasCompletionWindow: true);

        var reopened = state.HandleTextEntered("\n");

        Assert.False(reopened);
    }

    [Fact]
    public void HandleTextEntered_NewlineWithoutArmedReopen_ReturnsFalse()
    {
        var state = new CompletionInteractionState();

        var reopened = state.HandleTextEntered("\n");

        Assert.False(reopened);
    }
}
