using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Tariffs;

/// <summary>A price of a tariff component that applies from a given date onwards.</summary>
public class TariffRate : SyncEntity, IVersioned
{
    /// <summary>Owning component.</summary>
    public Guid ComponentId { get; set; }

    /// <summary>First date on which this rate applies.</summary>
    public DateOnly ValidFrom { get; set; }

    /// <summary>Price per unit / m² / resident / month. Ignored when <see cref="Tiers"/> is not empty.</summary>
    public decimal Price { get; set; }

    /// <summary>True when the quoted price already includes VAT.</summary>
    public bool PriceIncludesVat { get; set; }

    /// <summary>Currency of the price.</summary>
    public Currency Currency { get; set; }

    /// <summary>Stepped price, ordered by upper bound; the last tier is unlimited.</summary>
    public List<TariffTier> Tiers { get; set; } = [];

    /// <summary>Period over which tier thresholds accumulate; required when there are tiers.</summary>
    public TierPeriod? TierPeriod { get; set; }

    /// <summary>Free-form notes.</summary>
    public string? Notes { get; set; }
}
