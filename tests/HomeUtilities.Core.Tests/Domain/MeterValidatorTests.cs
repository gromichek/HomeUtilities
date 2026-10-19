using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using static HomeUtilities.Core.Tests.Fakes.MeterBuilder;

namespace HomeUtilities.Core.Tests.Domain;

public class MeterValidatorTests
{
    private static Meter Valid() => Meter(D(2020, 1, 1), digits: 5);

    private static string[] Codes(Meter m) => [.. MeterValidator.Validate(m).Errors.Select(e => e.Code)];

    [Fact]
    public void Valid_meter_passes() => Assert.Empty(Codes(Valid()));

    [Fact]
    public void Empty_property_fails()
    {
        var m = Valid();
        m.PropertyId = Guid.Empty;
        Assert.Equal([ErrorCodes.MeterPropertyRequired], Codes(m));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(12, true)]
    [InlineData(13, false)]
    public void Digits_range(int digits, bool ok)
    {
        var m = Valid();
        m.Digits = digits;
        Assert.Equal(ok, MeterValidator.Validate(m).IsValid);
    }

    [Fact]
    public void Null_digits_passes()
    {
        var m = Valid();
        m.Digits = null;
        Assert.Empty(Codes(m));
    }

    [Fact]
    public void Removed_before_installed_fails_and_same_day_passes()
    {
        var m = Valid();
        m.RemovedOn = D(2020, 1, 1);
        Assert.Empty(Codes(m));
        m.RemovedOn = D(2019, 12, 31);
        Assert.Equal([ErrorCodes.MeterRemovedBeforeInstalled], Codes(m));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(600, true)]
    [InlineData(601, false)]
    public void Verification_interval_range(int months, bool ok)
    {
        var m = Valid();
        m.VerificationIntervalMonths = months;
        Assert.Equal(ok, MeterValidator.Validate(m).IsValid);
    }

    [Fact]
    public void No_registers_fails()
    {
        var m = Valid();
        m.Registers.Clear();
        Assert.Equal([ErrorCodes.MeterRegisterRequired], Codes(m));
    }

    [Fact]
    public void Only_deleted_registers_fails()
    {
        var m = Valid();
        m.Registers[0].IsDeleted = true;
        Assert.Equal([ErrorCodes.MeterRegisterRequired], Codes(m));
    }

    [Fact]
    public void Duplicate_register_fails_but_deleted_duplicate_is_ignored()
    {
        var m = Meter(D(2020, 1, 1), 5, null, RegisterKind.Day, RegisterKind.Night);
        m.Registers[1].Kind = RegisterKind.Day;
        Assert.Equal([ErrorCodes.MeterDuplicateRegister], Codes(m));
        m.Registers[1].IsDeleted = true;
        Assert.Empty(Codes(m));
    }

    [Fact]
    public void Day_night_export_passes()
    {
        var m = Meter(D(2020, 1, 1), 6, null, RegisterKind.Day, RegisterKind.Night, RegisterKind.Export);
        m.Resource = MeterResource.Electricity;
        m.Unit = MeterUnit.KWh;
        Assert.Empty(Codes(m));
    }

    [Fact]
    public void Export_kind_requires_export_direction_and_vice_versa()
    {
        var m = Valid();
        m.Registers[0].Kind = RegisterKind.Export;
        Assert.Equal([ErrorCodes.MeterExportDirectionMismatch], Codes(m));
        m.Registers[0].Kind = RegisterKind.Total;
        m.Registers[0].Direction = EnergyDirection.Export;
        Assert.Equal([ErrorCodes.MeterExportDirectionMismatch], Codes(m));
    }

    [Theory]
    [InlineData(MeterResource.ColdWater, MeterUnit.M3, true)]
    [InlineData(MeterResource.ColdWater, MeterUnit.KWh, false)]
    [InlineData(MeterResource.HotWater, MeterUnit.M3, true)]
    [InlineData(MeterResource.HotWater, MeterUnit.Gcal, false)]
    [InlineData(MeterResource.Gas, MeterUnit.M3, true)]
    [InlineData(MeterResource.Gas, MeterUnit.KWh, false)]
    [InlineData(MeterResource.Electricity, MeterUnit.KWh, true)]
    [InlineData(MeterResource.Electricity, MeterUnit.M3, false)]
    [InlineData(MeterResource.Heat, MeterUnit.MWh, true)]
    [InlineData(MeterResource.Heat, MeterUnit.Gcal, true)]
    [InlineData(MeterResource.Heat, MeterUnit.KWh, true)]
    [InlineData(MeterResource.Heat, MeterUnit.M3, false)]
    [InlineData(MeterResource.Other, MeterUnit.M3, true)]
    [InlineData(MeterResource.Other, MeterUnit.Gcal, true)]
    public void Unit_compatibility(MeterResource resource, MeterUnit unit, bool ok)
    {
        var m = Valid();
        m.Resource = resource;
        m.Unit = unit;
        Assert.Equal(ok, MeterValidator.Validate(m).IsValid);
    }

    [Fact]
    public void ContractMeter_rules()
    {
        var ok = new ContractMeter { ContractId = Guid.CreateVersion7(), MeterId = Guid.CreateVersion7(), ValidFrom = D(2024, 1, 1), ValidTo = D(2024, 1, 1) };
        Assert.True(ContractMeterValidator.Validate(ok).IsValid);

        var bad = new ContractMeter { ValidFrom = D(2024, 1, 2), ValidTo = D(2024, 1, 1) };
        Assert.Equal(
            [ErrorCodes.ContractMeterContractRequired, ErrorCodes.ContractMeterMeterRequired, ErrorCodes.ContractMeterValidToBeforeValidFrom],
            ContractMeterValidator.Validate(bad).Errors.Select(e => e.Code));
    }
}
