using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tests.Fakes;
using static HomeUtilities.Core.Tests.Fakes.MeterBuilder;

namespace HomeUtilities.Core.Tests.Domain;

public class MeterReplacementTests
{
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Single_register_replacement()
    {
        var old = Meter(D(2009, 1, 1), 5);
        old.Location = "bathroom";
        old.Add(D(2015, 3, 1), 190);

        var result = MeterReplacement.Replace(old, D(2015, 4, 30), 202m, 0m, _clock);

        Assert.Equal(D(2015, 4, 30), old.RemovedOn);
        var final = Assert.Single(result.FinalReadings);
        Assert.Equal((ReadingKind.Final, 202m, D(2015, 4, 30)), (final.Kind, final.Value, final.Date));
        Assert.Contains(final, old.Registers[0].Readings);
        Assert.Equal(old.Registers[0].Id, final.RegisterId);

        var n = result.NewMeter;
        Assert.Equal(old.Id, n.ReplacedMeterId);
        Assert.Equal(old.PropertyId, n.PropertyId);
        Assert.Equal((old.Resource, old.Unit, old.Location), (n.Resource, n.Unit, n.Location));
        Assert.Equal(D(2015, 4, 30), n.InstalledOn);
        var reg = Assert.Single(n.Registers);
        Assert.Equal((RegisterKind.Total, EnergyDirection.Import, n.Id), (reg.Kind, reg.Direction, reg.MeterId));
        var initial = Assert.Single(result.InitialReadings);
        Assert.Equal((ReadingKind.Initial, 0m, D(2015, 4, 30)), (initial.Kind, initial.Value, initial.Date));
        Assert.Equal(reg.Id, initial.RegisterId);
        Assert.Contains(initial, reg.Readings);
    }

    [Fact]
    public void Dual_tariff_replacement()
    {
        var old = Meter(D(2010, 1, 1), 6, null, RegisterKind.Day, RegisterKind.Night);
        old.Resource = MeterResource.Electricity;
        old.Unit = MeterUnit.KWh;
        var values = new Dictionary<RegisterKind, (decimal Final, decimal Initial)>
        {
            [RegisterKind.Day] = (5000m, 0m),
            [RegisterKind.Night] = (3000m, 1m),
        };

        var result = MeterReplacement.Replace(old, D(2024, 5, 1), values, _clock);

        Assert.Equal(2, result.FinalReadings.Count);
        Assert.Equal(2, result.InitialReadings.Count);
        Assert.Equal([RegisterKind.Day, RegisterKind.Night], result.NewMeter.Registers.Select(r => r.Kind));
        var nightInitial = result.NewMeter.Registers[1].Readings.Single();
        Assert.Equal(1m, nightInitial.Value);
        Assert.Equal(3000m, old.Registers[1].Readings.Single().Value);
    }

    [Fact]
    public void Timestamps_are_set()
    {
        var old = Meter(D(2009, 1, 1), 5);
        var result = MeterReplacement.Replace(old, D(2015, 4, 30), 202m, 0m, _clock);

        Assert.Equal(_clock.UtcNow, old.UpdatedAt);
        Assert.Equal(_clock.UtcNow, result.NewMeter.CreatedAt);
        Assert.Equal(_clock.UtcNow, result.NewMeter.Registers[0].UpdatedAt);
        Assert.Equal(_clock.UtcNow, result.FinalReadings[0].CreatedAt);
        Assert.Equal(_clock.UtcNow, result.InitialReadings[0].CreatedAt);
    }

    [Fact]
    public void Values_must_match_registers()
    {
        var old = Meter(D(2010, 1, 1), 6, null, RegisterKind.Day, RegisterKind.Night);
        Assert.Throws<ArgumentException>(() => MeterReplacement.Replace(old, D(2024, 5, 1), 1m, 0m, _clock));
    }

    [Fact]
    public void Already_removed_meter_throws()
    {
        var old = Meter(D(2010, 1, 1), 6);
        old.RemovedOn = D(2020, 1, 1);
        Assert.Throws<InvalidOperationException>(() => MeterReplacement.Replace(old, D(2024, 5, 1), 1m, 0m, _clock));
    }
}
