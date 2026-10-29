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

    /// <summary>"Session (5h): ok · resets 7:00 PM"; a window that is not "allowed" is spelled out so it stands out.</summary>
    public static string DescribeRateLimit(AiRateLimit limit, DateTime nowLocal)
    {
        var state = limit.IsAllowed ? "ok" : limit.Status.Replace('_', ' ');
        var text = $"{DescribeWindow(limit.WindowType)}: {state}";

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
