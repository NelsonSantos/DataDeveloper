using System.Collections.Generic;
using System.Linq;

namespace DataDeveloper.Models;

/// <summary>Remembers the order in which items were last used, most recent first.</summary>
public sealed class RecentUseList<T> where T : class
{
    private readonly List<T> _items = new();

    public void Touch(T item)
    {
        _items.Remove(item);
        _items.Insert(0, item);
    }

    public void Remove(T item) => _items.Remove(item);

    /// <summary>
    /// <paramref name="items"/> ordered by recent use; items never used keep their relative order at the end.
    /// Items no longer in <paramref name="items"/> are ignored.
    /// </summary>
    public IReadOnlyList<T> Order(IReadOnlyCollection<T> items) =>
        _items.Where(items.Contains).Concat(items.Where(item => !_items.Contains(item))).ToList();
}
