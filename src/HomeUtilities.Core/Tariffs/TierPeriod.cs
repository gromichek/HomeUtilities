namespace HomeUtilities.Core.Tariffs;

/// <summary>Period over which the quantity of a tiered tariff accumulates.</summary>
public enum TierPeriod
{
    /// <summary>Thresholds apply per month.</summary>
    Month = 0,

    /// <summary>Thresholds apply per year.</summary>
    Year = 1,
}
