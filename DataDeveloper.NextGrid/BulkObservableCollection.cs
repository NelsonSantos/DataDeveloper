using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace DataDeveloper.NextGrid;

public class BulkObservableCollection<T> : ObservableCollection<T>
{
    private int _updateDepth;
    private bool _hasCollectionChanges;
    private bool _hasCountChange;
    private bool _hasIndexerChange;
    private NotifyCollectionChangedEventArgs? _deferredCollectionChangedEventArgs;

    public void BeginUpdate()
    {
        _updateDepth++;
    }

    public void EndUpdate()
    {
        if (_updateDepth == 0)
            return;

        _updateDepth--;
        if (_updateDepth > 0 || !_hasCollectionChanges)
            return;

        FlushDeferredNotifications();
    }

    public void AddRange(IEnumerable<T> items)
    {
        var materializedItems = items.ToList();
        if (materializedItems.Count == 0)
            return;

        var startIndex = Count;
        BeginUpdate();
        try
        {
            foreach (var item in materializedItems)
                Items.Add(item);

            if (_updateDepth > 0)
            {
                _hasCollectionChanges = true;
                _hasCountChange = true;
                _hasIndexerChange = true;
                _deferredCollectionChangedEventArgs = new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add,
                    materializedItems,
                    startIndex);
            }
        }
        finally
        {
            EndUpdate();
        }
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_updateDepth > 0)
        {
            _hasCollectionChanges = true;
            _deferredCollectionChangedEventArgs = MergeDeferredEvent(_deferredCollectionChangedEventArgs, e);
            return;
        }

        base.OnCollectionChanged(e);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (_updateDepth > 0)
        {
            if (e.PropertyName == nameof(Count))
                _hasCountChange = true;

            if (e.PropertyName == "Item[]")
                _hasIndexerChange = true;

            return;
        }

        base.OnPropertyChanged(e);
    }

    private void FlushDeferredNotifications()
    {
        if (_hasCountChange)
            base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));

        if (_hasIndexerChange)
            base.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));

        base.OnCollectionChanged(_deferredCollectionChangedEventArgs ??
                                 new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

        _hasCollectionChanges = false;
        _hasCountChange = false;
        _hasIndexerChange = false;
        _deferredCollectionChangedEventArgs = null;
    }

    private static NotifyCollectionChangedEventArgs MergeDeferredEvent(
        NotifyCollectionChangedEventArgs? current,
        NotifyCollectionChangedEventArgs next)
    {
        if (current is null)
            return next;

        if (current.Action == NotifyCollectionChangedAction.Add &&
            next.Action == NotifyCollectionChangedAction.Add)
        {
            return new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
        }

        return new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
    }
}
