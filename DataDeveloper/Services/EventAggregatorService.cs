using System;
using System.Collections.Generic;
using System.Linq;
using DataDeveloper.Interfaces;

namespace DataDeveloper.Services;

public class EventAggregatorService : IEventAggregatorService
{
    private readonly List<Subscription> _subscriptions = new();
    private readonly object _lock = new();

    public IDisposable Subscribe<T>(object subscriber, Action<T> handler, Func<T, bool>? filter = null)
    {
        var subscription = new Subscription(
            typeof(T),
            new WeakReference(subscriber),
            msg =>
            {
                if (msg is T casted && (filter?.Invoke(casted) ?? true))
                    handler(casted);
            });

        lock (_lock)
        {
            _subscriptions.Add(subscription);
        }

        return new SubscriptionToken(() => Unsubscribe(subscription));
    }

    public void Publish<T>(T message)
    {
        List<Subscription> snapshot;

        lock (_lock)
        {
            snapshot = _subscriptions.ToList();
        }

        var dead = new List<Subscription>();

        foreach (var sub in snapshot)
        {
            if (sub.Type == typeof(T))
            {
                if (sub.Target.IsAlive)
                {
                    try
                    {
                        sub.Handler(message!);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Handler error: {ex.Message}");
                    }
                }
                else
                {
                    dead.Add(sub);
                }
            }
        }

        if (dead.Any())
        {
            lock (_lock)
            {
                foreach (var d in dead)
                    _subscriptions.Remove(d);
            }
        }
    }

    public void UnsubscribeAllFor(object subscriber)
    {
        lock (_lock)
        {
            _subscriptions.RemoveAll(s => !s.Target.IsAlive || s.Target.Target == subscriber);
        }
    }

    public void Unsubscribe(Subscription subscription)
    {
        lock (_lock)
        {
            _subscriptions.Remove(subscription);
        }
    }


    private class SubscriptionToken : IDisposable
    {
        private Action? _unsubscribe;
        public SubscriptionToken(Action unsubscribe) => _unsubscribe = unsubscribe;

        public void Dispose()
        {
            _unsubscribe?.Invoke();
            _unsubscribe = null;
        }
    }
}
