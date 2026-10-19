using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.Core.Tests.Tariffs;

public class TariffComponentValidatorTests
{
    private static TariffComponent Valid() => new()
    {
        ContractId = Guid.CreateVersion7(),
        Name = "Water supply",
        ChargeKind = ServiceKind.ColdWater,
        Basis = ChargeBasis.PerUnit,
        VatCode = "LV-STANDARD",
        Rates = [new() { ValidFrom = new(2025, 1, 1), Price = 1.41m }],
    };

    private static string[] Codes(TariffComponent c) => [.. TariffComponentValidator.Validate(c).Errors.Select(e => e.Code)];

    private static void AddTiers(TariffComponent c, params TariffTier[] tiers)
    {
        c.Rates[0].Tiers = [.. tiers];
        c.Rates[0].TierPeriod = TierPeriod.Year;
    }

    [Fact]
    public void Valid_component_passes() => Assert.True(TariffComponentValidator.Validate(Valid()).IsValid);

    [Fact]
    public void Blank_name_fails()
    {
        var c = Valid();
        c.Name = "  ";
        Assert.Equal([ErrorCodes.TariffNameRequired], Codes(c));
    }

    [Fact]
    public void Name_of_100_passes_and_101_fails()
    {
        var c = Valid();
        c.Name = new string('x', 100);
        Assert.True(TariffComponentValidator.Validate(c).IsValid);
        c.Name = new string('x', 101);
        Assert.Equal([ErrorCodes.TariffNameTooLong], Codes(c));
    }

    [Fact]
    public void Empty_contract_fails()
    {
        var c = Valid();
        c.ContractId = Guid.Empty;
        Assert.Equal([ErrorCodes.TariffContractRequired], Codes(c));
    }

    [Fact]
    public void Missing_vat_code_fails_unless_override_is_set()
    {
        var c = Valid();
        c.VatCode = string.Empty;
        Assert.Equal([ErrorCodes.TariffVatRequired], Codes(c));
        c.VatPercentOverride = 12m;
        Assert.True(TariffComponentValidator.Validate(c).IsValid);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    public void Override_out_of_range_fails(double percent)
    {
        var c = Valid();
        c.VatPercentOverride = (decimal)percent;
        Assert.Equal([ErrorCodes.VatPercentOutOfRange], Codes(c));
    }

    [Fact]
    public void No_active_rate_fails()
    {
        var c = Valid();
        Assert.Equal([ErrorCodes.TariffRateRequired], Codes(new TariffComponent { ContractId = c.ContractId, Name = "x", VatCode = "ZERO" }));
        c.Rates[0].IsDeleted = true;
        Assert.Equal([ErrorCodes.TariffRateRequired], Codes(c));
    }

    [Fact]
    public void Duplicate_valid_from_fails()
    {
        var c = Valid();
        c.Rates.Add(new() { ValidFrom = new(2025, 1, 1), Price = 2m });
        Assert.Equal([ErrorCodes.DuplicateValidFrom], Codes(c));
    }

    [Fact]
    public void Negative_price_fails_and_zero_passes()
    {
        var c = Valid();
        c.Rates[0].Price = 0m;
        Assert.True(TariffComponentValidator.Validate(c).IsValid);
        c.Rates[0].Price = -0.01m;
        Assert.Equal([ErrorCodes.TariffPriceNegative], Codes(c));
    }

    [Fact]
    public void Valid_tiers_pass()
    {
        var c = Valid();
        AddTiers(c, new(100m, 0.1065m), new(200m, 0.09m), new(null, 0.0818m));
        Assert.True(TariffComponentValidator.Validate(c).IsValid);
    }

    [Fact]
    public void Tiers_without_tier_period_fail()
    {
        var c = Valid();
        AddTiers(c, new(100m, 0.1m), new(null, 0.08m));
        c.Rates[0].TierPeriod = null;
        Assert.Equal([ErrorCodes.TariffTierPeriodRequired], Codes(c));
    }

    [Fact]
    public void Last_tier_must_be_unlimited()
    {
        var c = Valid();
        AddTiers(c, new(100m, 0.1m), new(200m, 0.08m));
        Assert.Equal([ErrorCodes.TariffTierLastNotUnlimited], Codes(c));
    }

    [Fact]
    public void Unlimited_tier_before_the_last_fails()
    {
        var c = Valid();
        AddTiers(c, new(null, 0.1m), new(null, 0.08m));
        Assert.Equal([ErrorCodes.TariffTierOrderInvalid], Codes(c));
    }

    [Fact]
    public void Descending_or_equal_tier_bounds_fail()
    {
        var c = Valid();
        AddTiers(c, new(200m, 0.1m), new(100m, 0.09m), new(null, 0.08m));
        Assert.Equal([ErrorCodes.TariffTierOrderInvalid], Codes(c));
        AddTiers(c, new(100m, 0.1m), new(100m, 0.09m), new(null, 0.08m));
        Assert.Equal([ErrorCodes.TariffTierOrderInvalid], Codes(c));
    }

    [Fact]
    public void Negative_tier_price_fails()
    {
        var c = Valid();
        AddTiers(c, new(100m, -0.1m), new(null, 0.08m));
        Assert.Equal([ErrorCodes.TariffPriceNegative], Codes(c));
    }

    [Theory]
    [InlineData(ChargeBasis.PerArea)]
    [InlineData(ChargeBasis.PerResident)]
    [InlineData(ChargeBasis.FixedMonthly)]
    [InlineData(ChargeBasis.FromBill)]
    public void Register_kinds_and_meters_are_only_allowed_for_per_unit_and_net_export(ChargeBasis basis)
    {
        var c = Valid();
        c.Basis = basis;
        c.RegisterKinds = [RegisterKind.Day];
        Assert.Equal([ErrorCodes.TariffSelectionNotAllowed], Codes(c));

        c.RegisterKinds = [];
        c.MeterIds = [Guid.CreateVersion7()];
        Assert.Equal([ErrorCodes.TariffSelectionNotAllowed], Codes(c));

        c.MeterIds = [];
        Assert.True(TariffComponentValidator.Validate(c).IsValid);
    }

    [Fact]
    public void Per_unit_allows_register_kinds_and_meters()
    {
        var c = Valid();
        c.RegisterKinds = [RegisterKind.Day];
        c.MeterIds = [Guid.CreateVersion7()];
        Assert.True(TariffComponentValidator.Validate(c).IsValid);
    }

    [Fact]
    public void Net_export_requires_export_register_or_none()
    {
        var c = Valid();
        c.Basis = ChargeBasis.NetExport;
        Assert.True(TariffComponentValidator.Validate(c).IsValid);
        c.RegisterKinds = [RegisterKind.Export];
        Assert.True(TariffComponentValidator.Validate(c).IsValid);
        c.RegisterKinds = [RegisterKind.Day];
        Assert.Equal([ErrorCodes.TariffNetExportRegister], Codes(c));
        c.RegisterKinds = [RegisterKind.Export, RegisterKind.Day];
        Assert.Equal([ErrorCodes.TariffNetExportRegister], Codes(c));
    }
}
