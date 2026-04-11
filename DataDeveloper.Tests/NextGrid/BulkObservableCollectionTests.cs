using System.Collections.Specialized;
using DataDeveloper.NextGrid;
using Xunit;

namespace DataDeveloper.Tests.NextGrid;

public sealed class BulkObservableCollectionTests
{
    [Fact]
    public void AddRange_RaisesSingleResetNotification()
    {
        var collection = new BulkObservableCollection<int>();
        var notifications = new List<NotifyCollectionChangedEventArgs>();

        collection.CollectionChanged += (_, args) => notifications.Add(args);

        collection.AddRange([1, 2, 3]);

        Assert.Equal([1, 2, 3], collection);
        Assert.Single(notifications);
        Assert.Equal(NotifyCollectionChangedAction.Add, notifications[0].Action);
        Assert.NotNull(notifications[0].NewItems);
        Assert.Equal(3, notifications[0].NewItems!.Count);
    }

    [Fact]
    public void BeginUpdate_EndUpdate_DefersNotificationsUntilCompleted()
    {
        var collection = new BulkObservableCollection<int>();
        var notifications = new List<NotifyCollectionChangedEventArgs>();

        collection.CollectionChanged += (_, args) => notifications.Add(args);

        collection.BeginUpdate();
        collection.Add(1);
        collection.Add(2);
        Assert.Empty(notifications);

        collection.EndUpdate();

        Assert.Equal([1, 2], collection);
        Assert.Single(notifications);
        Assert.Equal(NotifyCollectionChangedAction.Reset, notifications[0].Action);
    }
}
