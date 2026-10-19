using HomeUtilities.Core.Domain;
using static HomeUtilities.Core.Tests.Fakes.MeterBuilder;

namespace HomeUtilities.Core.Tests.Domain;

public class MeterTests
{
    [Fact]
    public void Verification_due_from_installation_plus_interval()
    {
        // 2015-04-30 + 72 months = 2021-04-30
        var m = new Meter { InstalledOn = D(2015, 4, 30), VerificationIntervalMonths = 72 };
        Assert.Equal(D(2021, 4, 30), m.GetVerificationDue());
    }

    [Fact]
    public void Verification_due_from_verified_on_overrides_installation()
    {
        // 2021-05-10 + 72 months = 2027-05-10
        var m = new Meter { InstalledOn = D(2015, 4, 30), VerifiedOn = D(2021, 5, 10), VerificationIntervalMonths = 72 };
        Assert.Equal(D(2027, 5, 10), m.GetVerificationDue());
    }

    [Fact]
    public void Explicit_next_verification_wins()
    {
        var m = new Meter { InstalledOn = D(2015, 4, 30), VerificationIntervalMonths = 72, NextVerificationDue = D(2030, 1, 1) };
        Assert.Equal(D(2030, 1, 1), m.GetVerificationDue());
    }

    [Fact]
    public void No_interval_gives_null()
    {
        Assert.Null(new Meter { InstalledOn = D(2015, 4, 30), VerifiedOn = D(2021, 5, 10) }.GetVerificationDue());
    }

    [Fact]
    public void IsInstalledOn_bounds_are_inclusive()
    {
        var m = new Meter { InstalledOn = D(2020, 1, 1), RemovedOn = D(2020, 12, 31) };
        Assert.False(m.IsInstalledOn(D(2019, 12, 31)));
        Assert.True(m.IsInstalledOn(D(2020, 1, 1)));
        Assert.True(m.IsInstalledOn(D(2020, 12, 31)));
        Assert.False(m.IsInstalledOn(D(2021, 1, 1)));
    }

    [Fact]
    public void IsInstalledOn_open_ended_and_deleted()
    {
        Assert.True(new Meter { InstalledOn = D(2020, 1, 1) }.IsInstalledOn(D(2099, 1, 1)));
        Assert.False(new Meter { InstalledOn = D(2020, 1, 1), IsDeleted = true }.IsInstalledOn(D(2020, 6, 1)));
    }

    [Fact]
    public void CreateDefault_is_total_import()
    {
        var id = Guid.CreateVersion7();
        var r = MeterRegister.CreateDefault(id);
        Assert.Equal(id, r.MeterId);
        Assert.Equal(RegisterKind.Total, r.Kind);
        Assert.Equal(EnergyDirection.Import, r.Direction);
    }

    [Fact]
    public void ContractMeter_IsActiveOn_bounds_are_inclusive()
    {
        var l = new ContractMeter { ValidFrom = D(2024, 1, 1), ValidTo = D(2024, 12, 31) };
        Assert.False(l.IsActiveOn(D(2023, 12, 31)));
        Assert.True(l.IsActiveOn(D(2024, 1, 1)));
        Assert.True(l.IsActiveOn(D(2024, 12, 31)));
        Assert.False(l.IsActiveOn(D(2025, 1, 1)));
        Assert.False(new ContractMeter { ValidFrom = D(2024, 1, 1), IsDeleted = true }.IsActiveOn(D(2024, 6, 1)));
    }
}
