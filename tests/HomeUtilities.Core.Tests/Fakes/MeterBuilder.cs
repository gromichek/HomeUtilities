using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tests.Fakes;

/// <summary>Builds synthetic meters with readings for tests.</summary>
public static class MeterBuilder
{
    public static DateOnly D(int y, int m, int d) => new(y, m, d);

    public static Meter Meter(DateOnly installedOn, int? digits = null, Guid? replaced = null, params RegisterKind[] kinds)
    {
        var meter = new Meter
        {
            PropertyId = Guid.CreateVersion7(),
            Resource = MeterResource.ColdWater,
            Unit = MeterUnit.M3,
            InstalledOn = installedOn,
            Digits = digits,
            ReplacedMeterId = replaced,
        };
        foreach (var kind in kinds.Length == 0 ? [RegisterKind.Total] : kinds)
        {
            meter.Registers.Add(new MeterRegister
            {
                MeterId = meter.Id,
                Kind = kind,
                Direction = kind == RegisterKind.Export ? EnergyDirection.Export : EnergyDirection.Import,
            });
        }

        return meter;
    }

    public static MeterReading Add(this Meter meter, DateOnly date, decimal value, ReadingKind kind = ReadingKind.Regular, RegisterKind register = RegisterKind.Total)
    {
        var reg = meter.Registers.Single(r => r.Kind == register);
        var reading = new MeterReading { RegisterId = reg.Id, Date = date, Value = value, Kind = kind };
        reg.Readings.Add(reading);
        return reading;
    }
}
