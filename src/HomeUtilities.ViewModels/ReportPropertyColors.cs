namespace HomeUtilities.ViewModels;

/// <summary>
/// Assigns each property a slot of the report colour palette. The slot depends only on the property's position in the
/// alphabetical list of all (non-deleted) properties, so a property keeps its colour whatever the filters show.
/// </summary>
public static class ReportPropertyColors
{
    /// <summary>The number of palette slots (<c>PropertyTint1..8</c> in Colors.xaml); the slots repeat after that.</summary>
    public const int PaletteSize = 8;

    /// <summary>Maps every property to its slot, 0 to <see cref="PaletteSize"/> - 1.</summary>
    /// <param name="allProperties">All properties (id and name), in any order.</param>
    public static IReadOnlyDictionary<Guid, int> Assign(IEnumerable<(Guid Id, string Name)> allProperties)
    {
        ArgumentNullException.ThrowIfNull(allProperties);

        var result = new Dictionary<Guid, int>();
        var position = 0;
        foreach (var property in allProperties.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Id))
        {
            result[property.Id] = position % PaletteSize;
            position++;
        }

        return result;
    }
}
