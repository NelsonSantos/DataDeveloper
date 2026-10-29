using System.Globalization;

namespace DataDeveloper.Data.Ai;

/// <summary>Turns the usage numbers reported by a CLI into the short texts the chat shows.</summary>
public static class AiUsageFormatter
{
    /// <summary>"1.2k in (900 cached) · 340 out · ≈ $0.004", prefixed with the model when known.</summary>
    public static string Describe(AiUsage usage)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(usage.Model))
            parts.Add(usage.Model!);

        var input = $"{FormatTokens(usage.TotalInputTokens)} in";
        if (usage.CacheReadTokens > 0)
            input += $" ({FormatTokens(usage.CacheReadTokens)} cached)";
        parts.Add(input);
        parts.Add($"{FormatTokens(usage.OutputTokens)} out");

        if (usage.CostUsd is { } cost)
            parts.Add($"≈ {FormatCost(cost)}");

        return string.Join(" · ", parts);
    }

    public static string DescribeRateLimit(AiRateLimit limit, DateTime nowLocal) =>
        $"{DescribeWindow(limit.WindowType)}: {DescribeRateLimitState(limit, nowLocal)}";

    /// <summary>"within the limit · resets Thu 12:00 AM"; a window that is not "allowed" is spelled out so it stands out.</summary>
    public static string DescribeRateLimitState(AiRateLimit limit, DateTime nowLocal)
    {
        var text = limit.IsAllowed ? "within the limit" : limit.Status.Replace('_', ' ');

        if (limit.IsUsingOverage)
            text += " · using extra usage";

        if (limit.ResetsAtUtc is { } resetsAtUtc)
        {
            var resetsAt = resetsAtUtc.ToLocalTime();
            var format = resetsAt.Date == nowLocal.Date ? "t" : "ddd t";
            text += $" · resets {resetsAt.ToString(format, CultureInfo.CurrentCulture)}";
        }

        return text;
    }

    /// <summary>Share of the context window in use, 0 to 1; 0 while the window is unknown.</summary>
    public static double ContextFraction(long usedTokens, long? window) =>
        window is > 0 ? Math.Clamp(usedTokens / (double)window.Value, 0, 1) : 0;

    /// <summary>"35.1k / 1.0M · 3%".</summary>
    public static string DescribeContext(long usedTokens, long window)
    {
        var percent = ContextFraction(usedTokens, window) * 100;
        var text = percent > 0 && percent < 1 ? "<1%" : $"{percent:0}%";
        return $"{FormatTokens(usedTokens)} / {FormatTokens(window)} · {text}";
    }

    public static string DescribeWindow(string? windowType) => windowType?.ToLowerInvariant() switch
    {
        "five_hour" => "Session (5h)",
        "seven_day" => "Weekly",
        null or "" => "Usage",
        var other => other.Replace('_', ' ')
    };

    public static string FormatTokens(long tokens) => tokens switch
    {
        < 1_000 => tokens.ToString(CultureInfo.InvariantCulture),
        < 100_000 => (tokens / 1_000d).ToString("0.0", CultureInfo.InvariantCulture) + "k",
        < 1_000_000 => (tokens / 1_000d).ToString("0", CultureInfo.InvariantCulture) + "k",
        _ => (tokens / 1_000_000d).ToString("0.0", CultureInfo.InvariantCulture) + "M"
    };

    public static string FormatCost(decimal cost) =>
        "$" + cost.ToString(cost < 0.01m ? "0.0000" : "0.00", CultureInfo.InvariantCulture);
}
