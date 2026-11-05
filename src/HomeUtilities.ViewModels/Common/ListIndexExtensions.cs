namespace HomeUtilities.ViewModels.Common;

/// <summary>Index lookup for the choice dialogs, which work with the index of the picked option.</summary>
public static class ListIndexExtensions
{
    /// <summary>Index of the first item matching <paramref name="match"/>, or -1 (nothing is marked in the dialog).</summary>
    public static int IndexWhere<T>(this IReadOnlyList<T> list, Func<T, bool> match)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(match);
        for (var i = 0; i < list.Count; i++)
        {
            if (match(list[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
