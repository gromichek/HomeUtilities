using HomeUtilities.Core.Consumption;
using static HomeUtilities.Core.Tests.Fakes.MeterBuilder;

namespace HomeUtilities.Core.Tests.Consumption;

public class MeterChainTests
{
    [Fact]
    public void Chain_from_middle_meter_returns_all_in_order_and_ignores_other_chains()
    {
        var a = Meter(D(2005, 1, 1));
        var b = Meter(D(2010, 1, 1), null, a.Id);
        var c = Meter(D(2020, 1, 1), null, b.Id);
        var other = Meter(D(2012, 1, 1));
        var otherNew = Meter(D(2022, 1, 1), null, other.Id);

        var chain = MeterChain.Build([c, other, a, otherNew, b], b.Id);

        Assert.Equal([a.Id, b.Id, c.Id], chain.Meters.Select(m => m.Id));
    }

    [Fact]
    public void Single_meter_chain()
    {
        var a = Meter(D(2005, 1, 1));
        Assert.Equal([a.Id], MeterChain.Build([a], a.Id).Meters.Select(m => m.Id));
    }

    [Fact]
    public void Unknown_meter_throws()
    {
        Assert.Throws<ArgumentException>(() => MeterChain.Build([Meter(D(2005, 1, 1))], Guid.CreateVersion7()));
    }
}
