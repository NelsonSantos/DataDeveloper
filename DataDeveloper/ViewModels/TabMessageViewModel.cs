using System;
using DataDeveloper.Enums;
using DataDeveloper.EventAggregators;
using DataDeveloper.Interfaces;
using DataDeveloper.Models;
using ReactiveUI.Fody.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.ViewModels;

public class TabMessageViewModel : BaseTabContent
{
    private readonly Guid _filterId;
    private readonly IEventAggregatorService _eventAggregatorService;
    
    public TabMessageViewModel(string name, bool canClose, Guid filterId, IServiceProvider serviceProvider) 
        : base(TabType.Message, name, canClose, serviceProvider)
    {
        _filterId = filterId;
        _eventAggregatorService = ServiceProvider.GetService<IEventAggregatorService>();
        _eventAggregatorService.Subscribe<ShowResultMessageEvent>(this, DisplayMessage, msg => msg.Id == _filterId);
    }
    [Reactive] public string Message { get; set; }

    private void DisplayMessage(ShowResultMessageEvent message)
    {
        this.Message = message.Message;
    }
}