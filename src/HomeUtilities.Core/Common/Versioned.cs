namespace HomeUtilities.Core.Common;

/// <summary>Lookup and validation helpers for time-versioned items. Deleted entities are ignored.</summary>
public static class Versioned
{
    /// <summary>Returns the item with the greatest ValidFrom not after <paramref name="date"/>, or null.</summary>
    public static T? At<T>(IEnumerable<T> items, DateOnly date)
        where T : class, IVersioned
    {
        ArgumentNullException.ThrowIfNull(items);

        T? best = null;
        foreach (var item in items)
        {
            if (IsActive(item) && item.ValidFrom <= date && (best is null || item.ValidFrom > best.ValidFrom))
            {
                best = item;
            }
        }

        return best;
    }

    /// <summary>Same as <see cref="At{T}"/>, with a bool result.</summary>
    public static bool TryAt<T>(IEnumerable<T> items, DateOnly date, out T? value)
        where T : class, IVersioned
    {
        value = At(items, date);
        return value is not null;
    }

    /// <summary>Reports <see cref="ErrorCodes.DuplicateValidFrom"/> for every ValidFrom shared by several items.</summary>
    public static ValidationResult Validate<T>(IEnumerable<T> items)
        where T : IVersioned
    {
        ArgumentNullException.ThrowIfNull(items);

        var errors = items
            .Where(IsActive)
            .GroupBy(item => item.ValidFrom)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key)
            .Select(group => new ValidationError(
                ErrorCodes.DuplicateValidFrom,
                nameof(IVersioned.ValidFrom),
                [group.Key]))
            .ToArray();

        return errors.Length == 0 ? ValidationResult.Ok : ValidationResult.Fail(errors);
    }

    private static bool IsActive<T>(T item) => item is not SyncEntity { IsDeleted: true };
}
