using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.Core.Tests.Tariffs;

public class TariffPricingTests
{
    private static TariffRate Tiered() => new()
    {
        Tiers = [new(100m, 0.1065m), new(null, 0.0818m)],
        TierPeriod = TierPeriod.Year,
        Price = 99m,
    };

    [Fact]
    public void No_tiers_gives_one_slice()
    {
        // 12.084 m3 * 1.41 EUR -> a single slice
        var slices = TariffPricing.Slice(new TariffRate { Price = 1.41m }, 12.084m);

        Assert.Equal([new PriceSlice(12.084m, 1.41m)], slices);
    }

    [Fact]
    public void Tiers_fill_in_order_and_ignore_rate_price() =>
        Assert.Equal([new PriceSlice(100m, 0.1065m), new PriceSlice(50m, 0.0818m)], TariffPricing.Slice(Tiered(), 150m));

    [Fact]
    public void Tiers_start_after_already_used() =>
        Assert.Equal([new PriceSlice(20m, 0.1065m), new PriceSlice(130m, 0.0818m)], TariffPricing.Slice(Tiered(), 150m, 80m));

    [Fact]
    public void Already_used_beyond_first_tier_uses_last_tier_only() =>
        Assert.Equal([new PriceSlice(150m, 0.0818m)], TariffPricing.Slice(Tiered(), 150m, 120m));

    [Fact]
    public void Quantity_inside_first_tier_gives_one_slice() =>
        Assert.Equal([new PriceSlice(40m, 0.1065m)], TariffPricing.Slice(Tiered(), 40m));

    [Fact]
    public void Three_tiers_skip_exhausted_ones()
    {
        var rate = new TariffRate { Tiers = [new(100m, 1m), new(200m, 2m), new(null, 3m)], TierPeriod = TierPeriod.Month };

        // used 150: 50 left in tier 2 (200-150), rest in tier 3
        Assert.Equal([new PriceSlice(50m, 2m), new PriceSlice(10m, 3m)], TariffPricing.Slice(rate, 60m, 150m));
    }

    [Fact]
    public void Zero_quantity_gives_no_slices()
    {
        Assert.Empty(TariffPricing.Slice(Tiered(), 0m));
        Assert.Empty(TariffPricing.Slice(new TariffRate { Price = 1m }, 0m));
    }

    [Fact]
    public void Negative_quantity_with_tiers_throws() =>
        Assert.Throws<ArgumentException>(() => TariffPricing.Slice(Tiered(), -5m));

    [Fact]
    public void Negative_quantity_without_tiers_gives_one_negative_slice() =>
        Assert.Equal([new PriceSlice(-5m, 0.05m)], TariffPricing.Slice(new TariffRate { Price = 0.05m }, -5m));

    [Fact]
    public void Vat_override_wins()
    {
        var component = new TariffComponent { VatCode = "NOPE", VatPercentOverride = 12m };

        Assert.Equal(12m, TariffPricing.ResolveVatPercent(component, LatvianVatDefaults.Create(), new(2026, 1, 1)));
    }

    [Fact]
    public void Vat_is_taken_from_catalog_by_code_and_date()
    {
        var component = new TariffComponent { VatCode = "LV-STANDARD" };

        Assert.Equal(22m, TariffPricing.ResolveVatPercent(component, LatvianVatDefaults.Create(), new(2011, 6, 1)));
    }

    [Fact]
    public void Unknown_vat_code_throws()
    {
        var component = new TariffComponent { VatCode = "NOPE" };

        Assert.Throws<InvalidOperationException>(() => TariffPricing.ResolveVatPercent(component, LatvianVatDefaults.Create(), new(2026, 1, 1)));
    }

    [Fact]
    public void Vat_code_without_version_on_date_throws()
    {
        var component = new TariffComponent { VatCode = "LV-REDUCED" };

        Assert.Throws<InvalidOperationException>(() => TariffPricing.ResolveVatPercent(component, LatvianVatDefaults.Create(), new(2000, 1, 1)));
    }
}
