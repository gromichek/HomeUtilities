using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>A property (apartment, house, cottage, ...) whose utilities are tracked.</summary>
public class Property : SyncEntity
{
    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Kind of the property.</summary>
    public PropertyKind Kind { get; set; }

    /// <summary>Postal address.</summary>
    public string? Address { get; set; }

    /// <summary>Free-form notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Position in lists.</summary>
    public int SortOrder { get; set; }

    /// <summary>Sold or no longer tracked; hidden by default, history is kept.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Time-versioned parameters.</summary>
    public List<PropertyParameter> Parameters { get; set; } = [];

    /// <summary>
    /// Value of the (non-custom) parameter of the given kind valid on <paramref name="date"/>, or null.
    /// </summary>
    public decimal? GetParameter(PropertyParameterKind kind, DateOnly date) =>
        Versioned.At(Parameters.Where(p => p.Kind == kind), date)?.Value;

    /// <summary>Value of the custom parameter with the given name valid on <paramref name="date"/>, or null.</summary>
    public decimal? GetParameter(string customName, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(customName);
        return Versioned.At(
            Parameters.Where(p => p.Kind == PropertyParameterKind.Custom && p.Name == customName),
            date)?.Value;
    }
}
