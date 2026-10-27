using DataDeveloper.Data.Ai;
using Xunit;

namespace DataDeveloper.Tests.Ai;

public class AiChatDomainTests
{
    [Fact]
    public void Segments_SplitProseAndCodeBlocks()
    {
        var segments = AiMessageSegments.Parse("Try this:\n```sql\nselect 1;\nselect 2;\n```\nThen run it.");

        Assert.Equal(3, segments.Count);
        Assert.False(segments[0].IsCode);
        Assert.Equal("Try this:", segments[0].Text);
        Assert.True(segments[1].IsCode);
        Assert.Equal("sql", segments[1].Language);
        Assert.Equal("select 1;\nselect 2;", segments[1].Text);
        Assert.Equal("Then run it.", segments[2].Text);
    }

    [Fact]
    public void Segments_TreatUnterminatedFenceAsCode()
    {
        var segments = AiMessageSegments.Parse("```\nselect 1");

        var segment = Assert.Single(segments);
        Assert.True(segment.IsCode);
        Assert.Null(segment.Language);
    }

    [Fact]
    public void Segments_PlainTextIsOneProseSegment()
    {
        Assert.Single(AiMessageSegments.Parse("just words"));
        Assert.Empty(AiMessageSegments.Parse("   "));
    }

    [Fact]
    public void Transcript_WithoutHistoryIsThePrompt()
    {
        Assert.Equal("hi", AiTranscriptFormatter.Format(new AiChatRequest("hi")));
    }

    [Fact]
    public void Transcript_ListsPreviousTurnsThenTheNewMessage()
    {
        var history = new[]
        {
            new AiChatMessage(AiChatRole.User, "q1", DateTime.UtcNow),
            new AiChatMessage(AiChatRole.Assistant, "a1", DateTime.UtcNow)
        };

        var text = AiTranscriptFormatter.Format(new AiChatRequest("q2", null, history));

        Assert.Contains("User: q1", text);
        Assert.Contains("Assistant: a1", text);
        Assert.EndsWith("User: q2", text);
    }

    [Fact]
    public void TrimHistory_KeepsTheMostRecentMessagesInOrder()
    {
        var history = Enumerable.Range(1, 30)
            .Select(i => new AiChatMessage(AiChatRole.User, $"m{i}", DateTime.UtcNow))
            .ToList();

        var trimmed = AiTranscriptFormatter.TrimHistory(history);

        Assert.Equal(AiTranscriptFormatter.MaxHistoryMessages, trimmed.Count);
        Assert.Equal("m30", trimmed[^1].Content);
        Assert.Equal("m19", trimmed[0].Content);
    }

    [Fact]
    public void TrimHistory_DropsOldTurnsWhenTheyAreHuge()
    {
        var big = new string('x', AiTranscriptFormatter.MaxHistoryCharacters);
        var history = new[]
        {
            new AiChatMessage(AiChatRole.User, big, DateTime.UtcNow),
            new AiChatMessage(AiChatRole.Assistant, "recent", DateTime.UtcNow)
        };

        var trimmed = AiTranscriptFormatter.TrimHistory(history);

        Assert.Equal("recent", Assert.Single(trimmed).Content);
    }

    [Fact]
    public void Composer_AppendsAttachmentsAndTruncatesHugeOnes()
    {
        var huge = new string('y', AiPromptComposer.MaxAttachmentCharacters + 10);

        var prompt = AiPromptComposer.Compose("fix", [new AiTextAttachment("a.sql", "select 1"), new AiTextAttachment("big.log", huge)]);

        Assert.StartsWith("fix", prompt);
        Assert.Contains("Attached \"a.sql\":", prompt);
        Assert.Contains("select 1", prompt);
        Assert.Contains("Attached \"big.log\" (truncated):", prompt);
        Assert.DoesNotContain(huge, prompt);
        Assert.Equal("fix", AiPromptComposer.Compose("fix", []));
    }

    [Fact]
    public void Composer_DescribesAttachmentsForHistoryWithoutTheirContent()
    {
        var text = AiPromptComposer.DescribeForHistory("why?", [new AiTextAttachment("a.sql", "secret")], ["dbo.Orders"]);

        Assert.Equal("why?\n\n(Attached: a.sql, dbo.Orders)", text);
    }
}
