using System;
using DataDeveloper.Enums;
using DataDeveloper.EventAggregators;
using DataDeveloper.Interfaces;
using DataDeveloper.Models;
using ReactiveUI.SourceGenerators;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.ViewModels;

public partial class TabMessageViewModel : BaseTabContent
{
    private readonly Guid _filterId;
    private readonly Guid _connectionId;
    private readonly IEventAggregatorService _eventAggregatorService;
    
    public TabMessageViewModel(string name, bool canClose, Guid filterId, IServiceProvider serviceProvider, Guid connectionId = default) 
        : base(TabType.Message, name, canClose, serviceProvider)
    {
        _filterId = filterId;
        _connectionId = connectionId;
        _eventAggregatorService = ServiceProvider.GetRequiredService<IEventAggregatorService>();
        _eventAggregatorService.Subscribe<ShowResultMessageEvent>(this, DisplayMessage, msg => msg.Id == _filterId);
    }
    [Reactive] private string _message = string.Empty;
    [Reactive(SetModifier = AccessModifier.Private)] private string? _failedStatement;

    /// <summary>True when the message is an execution error the AI assistant can be asked about.</summary>
    [Reactive(SetModifier = AccessModifier.Private)] private bool _canAskAi;

    public void AskAi()
    {
        if (FailedStatement is null)
            return;

        _eventAggregatorService.Publish(new AskAiEvent(
            _connectionId,
            "This statement failed with the error below. Explain what is wrong and suggest a fixed version.",
            "Failed statement and error",
            $"-- Statement\n{FailedStatement}\n\n-- Error\n{Message}"));
    }

    private void DisplayMessage(ShowResultMessageEvent message)
    {
        this.Message = message.Message;
        this.FailedStatement = message.FailedStatement;
        this.CanAskAi = message.FailedStatement is not null;
    }
}
