using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.SqLite;
using Xunit;

namespace DataDeveloper.Tests.Providers;

public class SqLiteTriggerParserTests
{
    [Theory]
    [InlineData("create trigger t after insert on orders begin select 1; end", "after", "insert")]
    [InlineData("create trigger t delete on orders begin select 1; end", "before", "delete")]
    [InlineData("CREATE TEMP TRIGGER IF NOT EXISTS main.t BEFORE UPDATE OF total, code ON orders FOR EACH ROW WHEN new.total > 0 BEGIN SELECT 1; END", "before", "update")]
    [InlineData("create trigger t instead of update on open_orders begin select 1; end", "instead of", "update")]
    public void CompleteFromDefinition_ReadsTimingAndEvent(string definition, string expectedTiming, string expectedEvents)
    {
        var trigger = new TriggerModel { Name = "t", Definition = definition };

        SqLiteTriggerParser.CompleteFromDefinition(trigger);

        Assert.Equal(expectedTiming, trigger.Timing);
        Assert.Equal(expectedEvents, trigger.Events);
    }

    [Fact]
    public void CompleteFromDefinition_LeavesTriggerWithoutDefinitionUnchanged()
    {
        var trigger = new TriggerModel { Name = "t" };

        SqLiteTriggerParser.CompleteFromDefinition(trigger);

        Assert.Equal(string.Empty, trigger.Timing);
        Assert.Equal(string.Empty, trigger.Events);
    }
}
