using DataDeveloper.Data.Ai;
using Xunit;

namespace DataDeveloper.Tests.Ai;

public class AiUsageFormatterTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(842, "842")]
    [InlineData(1_234, "1.2k")]
    [InlineData(27_454, "27.5k")]
    [InlineData(250_000, "250k")]
    [InlineData(1_500_000, "1.5M")]
    public void FormatTokens_UsesShortUnits(long tokens, string expected) =>
        Assert.Equal(expected, AiUsageFormatter.FormatTokens(tokens));

    [Theory]
    [InlineData(0.0727602, "$0.07")]
    [InlineData(0.004, "$0.0040")]
    public void FormatCost_ShowsMoreDigitsForTinyAmounts(double cost, string expected) =>
        Assert.Equal(expected, AiUsageFormatter.FormatCost((decimal)cost));

    [Fact]
    public void Describe_ShowsInputCachedOutputAndCost()
    {
        var usage = new AiUsage(2, 16_204, 11_248, 27, 0.0727602m, "claude-sonnet-5");

        Assert.Equal("claude-sonnet-5 \u00B7 27.5k in (16.2k cached) \u00B7 27 out \u00B7 \u2248 $0.07", AiUsageFormatter.Describe(usage));
    }

    [Fact]
    public void Describe_OmitsWhatIsUnknown()
    {
        Assert.Equal("900 in \u00B7 40 out", AiUsageFormatter.Describe(new AiUsage(900, 0, 0, 40)));
    }

    [Fact]
    public void Add_SumsTokensAndCostAndDropsAMixedModel()
    {
        var total = new AiUsage(1, 2, 3, 4, 0.5m, "a").Add(new AiUsage(10, 20, 30, 40, 0.25m, "b"));

        Assert.Equal(new AiUsage(11, 22, 33, 44, 0.75m, null), total);
    }

    [Fact]
    public void DescribeRateLimit_NamesTheWindowAndResetTime()
    {
        var resets = DateTime.UtcNow.AddHours(2);
        var now = DateTime.Now;

        var text = AiUsageFormatter.DescribeRateLimit(new AiRateLimit("allowed", resets, "five_hour"), now);

        Assert.StartsWith("Session (5h): ok \u00B7 resets ", text);
    }

    [Fact]
    public void DescribeRateLimit_SpellsOutALimitedWindowAndOverage()
    {
        var text = AiUsageFormatter.DescribeRateLimit(new AiRateLimit("rejected", null, "seven_day", IsUsingOverage: true), DateTime.Now);

        Assert.Equal("Weekly: rejected \u00B7 using extra usage", text);
    }
}
