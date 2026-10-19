using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Tariffs;

/// <summary>Price and VAT resolution for tariff components.</summary>
public static class TariffPricing
{
    /// <summary>
    /// Splits a quantity into slices with one unit price each.
    /// </summary>
    /// <param name="rate">The rate to apply.</param>
    /// <param name="quantity">Quantity to charge.</param>
    /// <param name="alreadyUsed">Quantity already consumed earlier in the same tier period.</param>
    /// <returns>Without tiers one slice at <see cref="TariffRate.Price"/>; with tiers the quantity spread over the
    /// tiers that remain after <paramref name="alreadyUsed"/>. A zero quantity gives no slices.</returns>
    /// <exception cref="ArgumentException">The quantity is negative and the rate has tiers.</exception>
    public static IReadOnlyList<PriceSlice> Slice(TariffRate rate, decimal quantity, decimal alreadyUsed = 0m)
    {
        ArgumentNullException.ThrowIfNull(rate);

        if (rate.Tiers.Count == 0)
        {
            return quantity == 0m ? [] : [new PriceSlice(quantity, rate.Price)];
        }

        if (quantity < 0m)
        {
            throw new ArgumentException("A negative quantity cannot be charged with tiers.", nameof(quantity));
        }

        var slices = new List<PriceSlice>();
        var remaining = quantity;
        var lower = 0m;
        foreach (var tier in rate.Tiers)
        {
            if (remaining <= 0m)
            {
                break;
            }

            var available = tier.UpToQuantity is { } upTo ? upTo - Math.Max(lower, alreadyUsed) : remaining;
            lower = tier.UpToQuantity ?? lower;
            if (available <= 0m)
            {
                continue;
            }

            var part = Math.Min(remaining, available);
            slices.Add(new PriceSlice(part, tier.Price));
            remaining -= part;
        }

        return slices;
    }

    /// <summary>VAT percent in effect for a component on a date: the override if set, otherwise the catalog entry.</summary>
    /// <exception cref="InvalidOperationException">The VAT code is unknown or has no version on <paramref name="date"/>.</exception>
    public static decimal ResolveVatPercent(TariffComponent component, IReadOnlyCollection<VatRate> catalog, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(catalog);

        if (component.VatPercentOverride is { } overridden)
        {
            return overridden;
        }

        var rate = catalog.FirstOrDefault(r => !r.IsDeleted && r.Code == component.VatCode)
            ?? throw new InvalidOperationException($"Unknown VAT code '{component.VatCode}'.");
        return rate.PercentAt(date)
            ?? throw new InvalidOperationException($"VAT code '{component.VatCode}' has no rate on {date:yyyy-MM-dd}.");
    }
}
