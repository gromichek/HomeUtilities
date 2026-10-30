using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tests.Domain;

public class MeterLinkRepairTests
{
    private static readonly DateOnly LinkedToday = new(2026, 10, 29);

    [Fact]
    public void A_link_made_today_for_a_meter_with_older_readings_starts_at_the_installation()
    {
        // meter installed 01.05.2004, contract from 01.05.2004, linked 29.10.2026, readings since 2004
        var meter = MeterWith(new DateOnly(2004, 5, 1), new DateOnly(2010, 6, 1));
        var contract = new Contract { Title = "Water", ValidFrom = new DateOnly(2004, 5, 1) };
        var link = Link(contract, meter, LinkedToday);

        var item = Assert.Single(MeterLinkRepair.Find([meter], [contract], [link]));

        Assert.Same(link, item.Link);
        Assert.Equal(new DateOnly(2004, 5, 1), item.NewValidFrom);
        Assert.Equal(LinkedToday, link.ValidFrom);
    }

    [Fact]
    public void A_later_contract_start_wins_over_the_installation()
    {
        var meter = MeterWith(new DateOnly(2004, 5, 1), new DateOnly(2012, 3, 1));
        var contract = new Contract { Title = "Water", ValidFrom = new DateOnly(2010, 1, 1) };

        var item = Assert.Single(MeterLinkRepair.Find([meter], [contract], [Link(contract, meter, LinkedToday)]));

        Assert.Equal(new DateOnly(2010, 1, 1), item.NewValidFrom);
    }

    [Fact]
    public void A_link_that_already_starts_at_the_installation_is_left_alone()
    {
        var meter = MeterWith(new DateOnly(2004, 5, 1), new DateOnly(2010, 6, 1));
        var contract = new Contract { Title = "Water", ValidFrom = new DateOnly(2004, 5, 1) };

        Assert.Empty(MeterLinkRepair.Find([meter], [contract], [Link(contract, meter, new DateOnly(2004, 5, 1))]));
    }

    [Fact]
    public void A_link_without_readings_before_it_is_left_alone()
    {
        var meter = MeterWith(new DateOnly(2004, 5, 1), new DateOnly(2026, 10, 29));
        var contract = new Contract { Title = "Water", ValidFrom = new DateOnly(2004, 5, 1) };

        Assert.Empty(MeterLinkRepair.Find([meter], [contract], [Link(contract, meter, LinkedToday)]));
    }

    [Fact]
    public void Readings_before_the_contract_start_do_not_count()
    {
        // contract from 01.01.2010; the only reading (2008) is outside the proposed start 01.01.2010
        var meter = MeterWith(new DateOnly(2004, 5, 1), new DateOnly(2008, 1, 1));
        var contract = new Contract { Title = "Water", ValidFrom = new DateOnly(2010, 1, 1) };

        Assert.Empty(MeterLinkRepair.Find([meter], [contract], [Link(contract, meter, LinkedToday)]));
    }

    [Fact]
    public void Only_the_first_link_of_a_pair_is_moved_and_a_later_link_is_not_touched()
    {
        var meter = MeterWith(new DateOnly(2004, 5, 1), new DateOnly(2010, 6, 1));
        var contract = new Contract { Title = "Water", ValidFrom = new DateOnly(2004, 5, 1) };
        var first = Link(contract, meter, new DateOnly(2004, 5, 1));
        first.ValidTo = new DateOnly(2020, 12, 31);
        var second = Link(contract, meter, new DateOnly(2021, 1, 1));

        Assert.Empty(MeterLinkRepair.Find([meter], [contract], [first, second]));
    }

    [Fact]
    public void A_deleted_link_and_a_link_of_an_unknown_meter_are_ignored()
    {
        var meter = MeterWith(new DateOnly(2004, 5, 1), new DateOnly(2010, 6, 1));
        var contract = new Contract { Title = "Water", ValidFrom = new DateOnly(2004, 5, 1) };
        var deleted = Link(contract, meter, LinkedToday);
        deleted.IsDeleted = true;
        var stranger = new ContractMeter { ContractId = contract.Id, MeterId = Guid.NewGuid(), ValidFrom = LinkedToday };

        Assert.Empty(MeterLinkRepair.Find([meter], [contract], [deleted, stranger]));
    }

    [Fact]
    public void A_replacement_meter_linked_on_its_installation_day_is_left_alone()
    {
        var meter = MeterWith(new DateOnly(2018, 3, 15), new DateOnly(2019, 1, 1));
        var contract = new Contract { Title = "Water", ValidFrom = new DateOnly(2004, 5, 1) };

        Assert.Empty(MeterLinkRepair.Find([meter], [contract], [Link(contract, meter, new DateOnly(2018, 3, 15))]));
    }

    private static Meter MeterWith(DateOnly installedOn, DateOnly readingDate)
    {
        var meter = new Meter { InstalledOn = installedOn };
        var register = MeterRegister.CreateDefault(meter.Id);
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = readingDate, Value = 10m });
        meter.Registers.Add(register);
        return meter;
    }

    private static ContractMeter Link(Contract contract, Meter meter, DateOnly from) =>
        new() { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = from };
}
