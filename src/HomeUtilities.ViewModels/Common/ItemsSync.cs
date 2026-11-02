using System.Collections.ObjectModel;

namespace HomeUtilities.ViewModels.Common;

/// <summary>
/// Updates the items of a list in place instead of replacing the collection: a <c>CollectionView</c> whose
/// <c>ItemsSource</c> is replaced jumps back to the top, one whose collection changes keeps its scroll position (T-028b).
/// </summary>
public static class ItemsSync
{
    /// <summary>
    /// Makes <paramref name="target"/> contain <paramref name="source"/> in the same order. Rows with the same key are
    /// replaced (they are new view model instances), vanished rows removed, new rows inserted, moved rows moved.
    /// </summary>
    public static void Update<T, TKey>(ObservableCollection<T> target, IReadOnlyList<T> source, Func<T, TKey> key)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(key);

        var wanted = new HashSet<TKey>(source.Select(key));
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(key(target[i])))
            {
                target.RemoveAt(i);
            }
        }

        for (var i = 0; i < source.Count; i++)
        {
            var id = key(source[i]);
            if (i < target.Count && EqualityComparer<TKey>.Default.Equals(key(target[i]), id))
            {
                target[i] = source[i];
                continue;
            }

            var existing = -1;
            for (var j = i + 1; j < target.Count; j++)
            {
                if (EqualityComparer<TKey>.Default.Equals(key(target[j]), id))
                {
                    existing = j;
                    break;
                }
            }

            if (existing >= 0)
            {
                target.RemoveAt(existing);
            }

            target.Insert(i, source[i]);
        }

        while (target.Count > source.Count)
        {
            target.RemoveAt(target.Count - 1);
        }
    }
}
