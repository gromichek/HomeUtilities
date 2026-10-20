using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels.Common;

/// <summary>Display names and colours of payment categories.</summary>
public static class CategoryNames
{
    /// <summary>The 12 swatches offered by the category editor.</summary>
    public static IReadOnlyList<string> Palette { get; } =
    [
        "#1E88E5", "#E53935", "#8E24AA", "#FB8C00", "#43A047", "#FDD835",
        "#00897B", "#6D4C41", "#3949AB", "#D81B60", "#7CB342", "#757575",
    ];

    /// <summary>The name set by the user; for a built-in category without one, the localized name of its service kind.</summary>
    public static string DisplayName(Category category, ILocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(localizer);

        if (!string.IsNullOrWhiteSpace(category.Name))
        {
            return category.Name;
        }

        return category.SystemKey is { } kind ? DefaultName(kind, localizer) : string.Empty;
    }

    /// <summary>The localized default name of a built-in category.</summary>
    public static string DefaultName(ServiceKind kind, ILocalizer localizer) => EnumText.Get(localizer, kind);

    /// <summary>The first palette colour no category uses yet; when all are used, the palette is cycled.</summary>
    public static string NextColor(IReadOnlyCollection<Category> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);

        var used = categories.Select(c => c.Color).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Palette.FirstOrDefault(c => !used.Contains(c)) ?? Palette[categories.Count % Palette.Count];
    }
}
