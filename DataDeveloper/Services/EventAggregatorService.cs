using System;
using System.Collections.Generic;
using System.Linq;
using DataDeveloper.Interfaces;

namespace DataDeveloper.Services;

public class EventAggregatorService : IEventAggregatorService
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public void Subscribe<TMessage>(Action<TMessage> handler)
    {
        var type = typeof(TMessage);
        if (!_handlers.ContainsKey(type))
            _handlers[type] = new List<Delegate>();

        _handlers[type].Add(handler);
    }

    public void Unsubscribe<TMessage>(Action<TMessage> handler)
    {
        var type = typeof(TMessage);
        if (_handlers.TryGetValue(type, out var list))
            list.Remove(handler);
    }

    public void Publish<TMessage>(TMessage message)
    {
        var type = typeof(TMessage);
        if (_handlers.TryGetValue(type, out var list))
        {
            foreach (var handler in list.OfType<Action<TMessage>>())
                handler(message);
        }
    }
}