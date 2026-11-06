namespace HomeUtilities.Core.Domain;

/// <summary>The categories that exist from the start: one per <see cref="ServiceKind"/>.</summary>
public static class BuiltInCategories
{
    // No pure status green / red: those colours mean "paid" and "overdue" (UI_GUIDELINES §8). Applies to new data only — the
    // colours of categories that already exist are never changed.
    private static readonly string[] Palette =
    [
        "#1E88E5", "#0097A7", "#8E24AA", "#C25E00", "#D81B60", "#B8860B", "#546E7A", "#6D4C41",
        "#00897B", "#3949AB", "#5E35B1", "#0288D1", "#AB47BC", "#7D8B00", "#8D6E63", "#757575",
    ];

    /// <summary>Creates one new category per <see cref="ServiceKind"/> value, in enum order, with fixed colours.</summary>
    public static IReadOnlyList<Category> Create() =>
        [.. Enum.GetValues<ServiceKind>().Select((kind, i) => new Category
        {
            SystemKey = kind,
            Name = null,
            Color = Palette[i % Palette.Length],
            SortOrder = i,
        })];

    /// <summary>Id of the non-deleted built-in category for <paramref name="kind"/>, or null if there is none.</summary>
    public static Guid? FindFor(ServiceKind kind, IEnumerable<Category> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        return categories.FirstOrDefault(c => !c.IsDeleted && c.SystemKey == kind)?.Id;
    }
}
