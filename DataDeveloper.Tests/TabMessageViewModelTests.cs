using Xunit;
using DataDeveloper.EventAggregators;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using DataDeveloper.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.Tests;

public class TabMessageViewModelTests
{
    private static (TabMessageViewModel Tab, IEventAggregatorService Events, Guid TabId, Guid ConnectionId) Create()
    {
        var events = new EventAggregatorService();
        var services = new ServiceCollection().AddSingleton<IEventAggregatorService>(events).BuildServiceProvider();
        var tabId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        return (new TabMessageViewModel("Message", false, tabId, services, connectionId), events, tabId, connectionId);
    }

    [Fact]
    public void ExecutionError_CanAskAi_AndPublishesStatementAndError()
    {
        var (tab, events, tabId, connectionId) = Create();
        AskAiEvent? asked = null;
        using var _ = events.Subscribe<AskAiEvent>(this, e => asked = e);

        events.Publish(new ShowResultMessageEvent(tabId, "Invalid column name 'x'", "select x from t"));
        Assert.True(tab.CanAskAi);

        tab.AskAi();

        Assert.NotNull(asked);
        Assert.Equal(connectionId, asked!.ConnectionId);
        Assert.Contains("select x from t", asked.AttachmentContent);
        Assert.Contains("Invalid column name 'x'", asked.AttachmentContent);
    }

    [Fact]
    public void OrdinaryMessage_CannotAskAi_AndPublishesNothing()
    {
        var (tab, events, tabId, _) = Create();
        AskAiEvent? asked = null;
        using var _ = events.Subscribe<AskAiEvent>(this, e => asked = e);

        events.Publish(new ShowResultMessageEvent(tabId, "1 record(s) affected"));
        tab.AskAi();

        Assert.False(tab.CanAskAi);
        Assert.Null(asked);
    }

    [Fact]
    public void NextMessage_ClearsTheErrorState()
    {
        var (tab, events, tabId, _) = Create();

        events.Publish(new ShowResultMessageEvent(tabId, "boom", "select 1"));
        events.Publish(new ShowResultMessageEvent(tabId, "Execution cancelled."));

        Assert.False(tab.CanAskAi);
    }
}
