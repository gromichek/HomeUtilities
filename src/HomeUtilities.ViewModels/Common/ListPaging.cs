namespace HomeUtilities.ViewModels.Common;

/// <summary>
/// Limits how many rows a long list puts on screen: the first <see cref="PageSize"/>, then <see cref="PageSize"/> more per
/// "Show more". Changing the filter (a different <c>key</c>) starts again from the first page; reloading with the same filter
/// keeps the rows the user already opened.
/// </summary>
public sealed class ListPaging
{
    /// <summary>Rows per page; a list never creates many more row views than this at once.</summary>
    public const int PageSize = 100;

    private string? _key;

    /// <summary>The number of rows to show now.</summary>
    public int Limit { get; private set; } = PageSize;

    /// <summary>Starts again from the first page when <paramref name="key"/> differs from the previous call.</summary>
    public void Track(string key)
    {
        if (_key != key)
        {
            _key = key;
            Limit = PageSize;
        }
    }

    /// <summary>Adds a page.</summary>
    public void ShowMore() => Limit += PageSize;
}
