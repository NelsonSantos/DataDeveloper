using System;

namespace DataDeveloper.Interfaces;

public interface IEventAggregatorService
{
    void Subscribe<TMessage>(Action<TMessage> handler);
    void Unsubscribe<TMessage>(Action<TMessage> handler);
    void Publish<TMessage>(TMessage message);
}