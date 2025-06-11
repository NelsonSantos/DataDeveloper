using System;

namespace DataDeveloper.Interfaces;

public interface IEventAggregatorService
{
    IDisposable Subscribe<T>(object subscriber, Action<T> handler, Func<T, bool>? filter = null);
    void Publish<T>(T message);
    void UnsubscribeAllFor(object subscriber);
    void Unsubscribe(Subscription subscription);
}

public record Subscription(Type Type, WeakReference Target, Action<object> Handler);
