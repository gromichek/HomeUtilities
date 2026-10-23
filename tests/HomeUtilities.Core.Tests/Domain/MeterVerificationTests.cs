using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tests.Domain;

public class MeterVerificationTests
{
    private static readonly DateOnly Today = new(2026, 10, 23);

    [Fact]
    public void Installed_2020_10_01_with_72_months_is_due_2026_10_01_and_overdue()
    {
        var meter = new Meter { InstalledOn = new DateOnly(2020, 10, 1), VerificationIntervalMonths = 72 };

        Assert.Equal(new DateOnly(2026, 10, 1), meter.GetVerificationDue());
        Assert.Equal(VerificationStatus.Overdue, MeterVerification.GetStatus(meter, Today));
    }

    [Fact]
    public void Verified_2021_01_15_with_72_months_is_due_2027_01_15_and_has_no_badge()
    {
        var meter = new Meter { InstalledOn = new DateOnly(2020, 10, 1), VerifiedOn = new DateOnly(2021, 1, 15), VerificationIntervalMonths = 72 };

        Assert.Equal(new DateOnly(2027, 1, 15), meter.GetVerificationDue());
        Assert.Equal(VerificationStatus.None, MeterVerification.GetStatus(meter, Today));
    }

    [Theory]
    [InlineData(2026, 12, 22, VerificationStatus.DueSoon)] // today + 60 days
    [InlineData(2026, 12, 23, VerificationStatus.None)] // today + 61 days
    [InlineData(2026, 10, 23, VerificationStatus.DueSoon)] // due today is not overdue yet
    [InlineData(2026, 10, 22, VerificationStatus.Overdue)]
    public void Explicit_due_date_is_classified_against_today(int year, int month, int day, VerificationStatus expected)
    {
        var meter = new Meter { InstalledOn = new DateOnly(2020, 1, 1), NextVerificationDue = new DateOnly(year, month, day) };

        Assert.Equal(expected, MeterVerification.GetStatus(meter, Today));
    }

    [Fact]
    public void No_due_date_or_removed_meter_has_no_status()
    {
        Assert.Equal(VerificationStatus.None, MeterVerification.GetStatus(new Meter { InstalledOn = new DateOnly(2020, 1, 1) }, Today));
        var removed = new Meter { InstalledOn = new DateOnly(2020, 1, 1), NextVerificationDue = new DateOnly(2020, 2, 1), RemovedOn = new DateOnly(2026, 1, 1) };
        Assert.Equal(VerificationStatus.None, MeterVerification.GetStatus(removed, Today));
    }

    [Theory]
    [InlineData(MeterResource.ColdWater, MeterUnit.M3)]
    [InlineData(MeterResource.HotWater, MeterUnit.M3)]
    [InlineData(MeterResource.Gas, MeterUnit.M3)]
    [InlineData(MeterResource.Electricity, MeterUnit.KWh)]
    [InlineData(MeterResource.Heat, MeterUnit.MWh)]
    [InlineData(MeterResource.Other, MeterUnit.M3)]
    public void Default_unit_is_the_first_valid_unit(MeterResource resource, MeterUnit expected)
    {
        Assert.Equal(expected, MeterUnitRules.DefaultUnit(resource));
        Assert.True(MeterUnitRules.IsCompatible(resource, expected));
    }

    [Fact]
    public void Heat_allows_three_units_and_water_only_cubic_metres()
    {
        Assert.Equal([MeterUnit.MWh, MeterUnit.Gcal, MeterUnit.KWh], MeterUnitRules.UnitsFor(MeterResource.Heat));
        Assert.Equal([MeterUnit.M3], MeterUnitRules.UnitsFor(MeterResource.HotWater));
        Assert.False(MeterUnitRules.IsCompatible(MeterResource.ColdWater, MeterUnit.KWh));
    }
}
