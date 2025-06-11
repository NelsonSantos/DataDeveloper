using System;

namespace DataDeveloper.EventAggregators;

public class ShowResultMessageEvent
{
    public ShowResultMessageEvent(Guid id, string message)
    {
        Id = id;
        Message = message;
    }

    public Guid Id { get; }
    public string Message { get; }
}