using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tests.Domain;

public class MeterLinkRulesTests
{
    private static readonly DateOnly Installed = new(2004, 5, 1);
    private static readonly DateOnly ContractFrom = new(2010, 1, 1);

    [Fact]
    public void A_new_link_starts_at_the_installation_when_the_contract_is_not_later()
    {
        // meter installed 01.05.2004, contract from 01.05.2004 -> link from 01.05.2004
        Assert.Equal(new DateOnly(2004, 5, 1), MeterLinkRules.DefaultStart(Installed, new DateOnly(2004, 5, 1)));
        Assert.Equal(new DateOnly(2004, 5, 1), MeterLinkRules.DefaultStart(Installed, new DateOnly(2001, 1, 1)));
    }

    [Fact]
    public void A_new_link_starts_with_the_contract_when_the_contract_is_later_than_the_installation()
    {
        Assert.Equal(new DateOnly(2010, 1, 1), MeterLinkRules.DefaultStart(Installed, ContractFrom));
    }

    [Fact]
    public void A_new_link_after_an_ended_link_starts_the_day_after_it_ended()
    {
        var ended = Link(new DateOnly(2004, 5, 1), new DateOnly(2026, 10, 29));

        Assert.Equal(new DateOnly(2026, 10, 30), MeterLinkRules.NewLinkStart(Installed, new DateOnly(2004, 5, 1), [ended]));
    }

    [Fact]
    public void A_new_link_ignores_deleted_links_and_keeps_the_default_when_the_end_is_earlier()
    {
        var deleted = Link(new DateOnly(2004, 5, 1), new DateOnly(2026, 10, 29));
        deleted.IsDeleted = true;
        var early = Link(new DateOnly(2001, 1, 1), new DateOnly(2003, 12, 31));

        Assert.Equal(new DateOnly(2010, 1, 1), MeterLinkRules.NewLinkStart(Installed, ContractFrom, [deleted, early]));
    }

    [Fact]
    public void A_valid_link_has_no_errors()
    {
        var check = MeterLinkRules.Check(Link(new DateOnly(2010, 1, 1), null), Installed, ContractFrom, []);

        Assert.True(check.IsValid);
        Assert.False(check.StartsBeforeContract);
    }

    [Fact]
    public void Starting_before_the_installation_is_an_error()
    {
        var check = MeterLinkRules.Check(Link(new DateOnly(2004, 4, 30), null), Installed, new DateOnly(2004, 1, 1), []);

        var error = Assert.Single(check.Errors);
        Assert.Equal(ErrorCodes.ContractMeterValidFromBeforeInstallation, error.Code);
        Assert.Equal(nameof(ContractMeter.ValidFrom), error.Field);
    }

    [Fact]
    public void Starting_before_the_contract_is_only_a_warning()
    {
        // contract from 01.01.2010, link from 01.05.2004 (installation): allowed, the owner is told
        var check = MeterLinkRules.Check(Link(new DateOnly(2004, 5, 1), null), Installed, ContractFrom, []);

        Assert.True(check.IsValid);
        Assert.True(check.StartsBeforeContract);
    }

    [Fact]
    public void Ending_before_the_start_is_an_error()
    {
        var check = MeterLinkRules.Check(Link(new DateOnly(2012, 1, 1), new DateOnly(2011, 12, 31)), Installed, ContractFrom, []);

        var error = Assert.Single(check.Errors);
        Assert.Equal(ErrorCodes.ContractMeterValidToBeforeValidFrom, error.Code);
    }

    [Fact]
    public void Overlapping_another_link_is_an_error()
    {
        var other = Link(new DateOnly(2010, 1, 1), new DateOnly(2015, 12, 31));
        var candidate = Link(new DateOnly(2015, 12, 31), null);

        var check = MeterLinkRules.Check(candidate, Installed, ContractFrom, [other]);

        var error = Assert.Single(check.Errors);
        Assert.Equal(ErrorCodes.ContractMeterOverlap, error.Code);
        Assert.Equal(new DateOnly(2010, 1, 1), Assert.Single(error.Args!));
    }

    [Fact]
    public void A_link_next_to_another_one_does_not_overlap()
    {
        var other = Link(new DateOnly(2010, 1, 1), new DateOnly(2015, 12, 31));

        var check = MeterLinkRules.Check(Link(new DateOnly(2016, 1, 1), null), Installed, ContractFrom, [other]);

        Assert.True(check.IsValid);
    }

    [Fact]
    public void An_open_ended_link_overlaps_a_later_one_and_the_link_itself_is_ignored()
    {
        var edited = Link(new DateOnly(2010, 1, 1), null);
        var later = Link(new DateOnly(2020, 1, 1), null);
        var deleted = Link(new DateOnly(2010, 1, 1), null);
        deleted.IsDeleted = true;

        Assert.False(MeterLinkRules.Check(edited, Installed, ContractFrom, [later]).IsValid);
        Assert.True(MeterLinkRules.Check(edited, Installed, ContractFrom, [edited, deleted]).IsValid);
    }

    private static ContractMeter Link(DateOnly from, DateOnly? to) =>
        new() { ContractId = Guid.NewGuid(), MeterId = Guid.NewGuid(), ValidFrom = from, ValidTo = to };
}
