using System;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Data.Services;
using DataDeveloper.EventAggregators;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using DataDeveloper.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DataDeveloper.Tests;

public class TabQueryEditorViewModelAskAiTests
{
    private readonly EventAggregatorService _events = new();
    private readonly SqlServerConnectionSettings _settings = new() { DatabaseType = DatabaseType.SqlServer, Id = Guid.NewGuid() };

    public TabQueryEditorViewModelAskAiTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<DatabaseProviderFactoryService>();
        DatabaseExtensionsMethods.SetServiceProvider(services.BuildServiceProvider());
    }

    private TabQueryEditorViewModel Create() =>
        new(_settings, "Query 1", file: null, canClose: true, new Provider(_events));

    [Theory]
    [InlineData(AiEditorAction.Explain, "Explain")]
    [InlineData(AiEditorAction.Fix, "corrected")]
    [InlineData(AiEditorAction.Optimize, "Optimize")]
    public void AskAi_SendsTheWholeEditor_WhenNothingIsSelected(AiEditorAction action, string expectedInPrompt)
    {
        AskAiEvent? asked = null;
        using var _ = _events.Subscribe<AskAiEvent>(this, e => asked = e);
        var editor = Create();
        editor.SqlStatement = "select 1";

        editor.AskAi(action);

        Assert.NotNull(asked);
        Assert.Equal(_settings.Id, asked!.ConnectionId);
        Assert.Equal("select 1", asked.AttachmentContent);
        Assert.Equal("Query 1", asked.AttachmentName);
        Assert.Contains(expectedInPrompt, asked.Prompt);
    }

    [Fact]
    public void AskAi_DoesNothing_WhenTheEditorIsEmpty()
    {
        AskAiEvent? asked = null;
        using var _ = _events.Subscribe<AskAiEvent>(this, e => asked = e);
        var editor = Create();

        Assert.False(editor.CanAskAi);
        editor.AskAi(AiEditorAction.Explain);

        Assert.Null(asked);
    }

    private sealed class Provider(IEventAggregatorService events) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IEventAggregatorService) ? events : throw new NotSupportedException(serviceType.ToString());
    }
}
