namespace DataDeveloper.Data.Ai;

public sealed record AiMessageSegment(bool IsCode, string Text, string? Language = null);

/// <summary>Splits an answer into prose and fenced code blocks so the UI can offer copy/insert on the code.</summary>
public static class AiMessageSegments
{
    public static IReadOnlyList<AiMessageSegment> Parse(string content)
    {
        var segments = new List<AiMessageSegment>();
        var prose = new List<string>();
        List<string>? code = null;
        string? language = null;

        foreach (var line in content.ReplaceLineEndings("\n").Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                if (code is null)
                {
                    FlushProse(segments, prose);
                    code = [];
                    var info = trimmed[3..].Trim();
                    language = info.Length > 0 ? info : null;
                }
                else
                {
                    segments.Add(new AiMessageSegment(true, string.Join('\n', code), language));
                    code = null;
                    language = null;
                }

                continue;
            }

            (code ?? prose).Add(line);
        }

        // An unterminated fence (answer cut off) still shows its code as code.
        if (code is not null)
            segments.Add(new AiMessageSegment(true, string.Join('\n', code), language));

        FlushProse(segments, prose);
        return segments;
    }

    private static void FlushProse(List<AiMessageSegment> segments, List<string> prose)
    {
        var text = string.Join('\n', prose).Trim();
        if (text.Length > 0)
            segments.Add(new AiMessageSegment(false, text));

        prose.Clear();
    }
}
