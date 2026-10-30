using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;

namespace HomeUtilities.Core.Tests.Import;

public class ImportLinkStartsTests
{
    [Fact]
    public void The_earliest_date_is_the_first_day_of_the_earliest_period_or_the_earliest_reading()
    {
        var plan = Plan(Row(new YearMonth(2005, 3)), Row(new YearMonth(2005, 6), new DateOnly(2005, 2, 20)));

        Assert.Equal(new DateOnly(2005, 2, 20), ImportLinkStarts.EarliestImportedDate(plan));
    }

    [Fact]
    public void Rows_with_errors_and_rows_that_create_nothing_do_not_count()
    {
        var failed = Row(new YearMonth(2001, 1));
        failed.Issues.Add(new ImportIssue(ImportIssueSeverity.Error, ImportIssueCodes.NumberInvalid, []));
        var empty = new PlannedRow { RowNumber = 9, Period = new YearMonth(2002, 1) };

        Assert.Null(ImportLinkStarts.EarliestImportedDate(Plan(failed, empty)));
    }

    [Fact]
    public void A_link_made_today_moves_to_the_installation_of_its_meter()
    {
        // meter installed 01.05.2004, contract from 01.05.2004, linked 29.10.2026, imported months from 05.2004
        var (contract, meter, link) = Setup(new DateOnly(2004, 5, 1), new DateOnly(2004, 5, 1), new DateOnly(2026, 10, 29));

        var move = Assert.Single(ImportLinkStarts.Find(Plan(Row(new YearMonth(2004, 5))), contract, [meter], [link]));

        Assert.Same(link, move.Link);
        Assert.Equal(new DateOnly(2004, 5, 1), move.NewValidFrom);
    }

    [Fact]
    public void The_new_contract_start_decides_when_it_is_later_than_the_installation()
    {
        // contract from 01.01.2010 -> link from 01.01.2010
        var (contract, meter, link) = Setup(new DateOnly(2004, 5, 1), new DateOnly(2010, 1, 1), new DateOnly(2026, 10, 29));

        var move = Assert.Single(ImportLinkStarts.Find(Plan(Row(new YearMonth(2010, 1))), contract, [meter], [link]));

        Assert.Equal(new DateOnly(2010, 1, 1), move.NewValidFrom);
    }

    [Fact]
    public void When_the_contract_start_moves_too_the_links_follow_the_new_start()
    {
        // contract from 01.01.2010 now, import from 03.2008, meter installed 01.05.2004 -> contract start 01.03.2008, link 01.03.2008
        var (contract, meter, link) = Setup(new DateOnly(2004, 5, 1), new DateOnly(2010, 1, 1), new DateOnly(2026, 10, 29));
        var row = Row(new YearMonth(2008, 3));
        row.Issues.Add(new ImportIssue(ImportIssueSeverity.Warning, ImportIssueCodes.PeriodBeforeContractStart, []));

        var plan = Plan(row);
        var move = Assert.Single(ImportLinkStarts.Find(plan, contract, [meter], [link]));

        Assert.Equal(new DateOnly(2008, 3, 1), plan.SuggestedContractStart);
        Assert.Equal(new DateOnly(2008, 3, 1), move.NewValidFrom);
    }

    [Fact]
    public void A_link_that_already_covers_the_import_is_left_alone()
    {
        var (contract, meter, link) = Setup(new DateOnly(2004, 5, 1), new DateOnly(2004, 5, 1), new DateOnly(2004, 5, 1));

        Assert.Empty(ImportLinkStarts.Find(Plan(Row(new YearMonth(2005, 1))), contract, [meter], [link]));
    }

    [Fact]
    public void A_link_that_cannot_start_earlier_is_left_alone_and_other_contracts_are_ignored()
    {
        // meter installed 01.01.2020: the link from 01.01.2020 is as early as it gets, whatever the sheet says
        var (contract, meter, link) = Setup(new DateOnly(2020, 1, 1), new DateOnly(2004, 5, 1), new DateOnly(2020, 1, 1));
        var foreign = new ContractMeter { ContractId = Guid.NewGuid(), MeterId = meter.Id, ValidFrom = new DateOnly(2026, 10, 29) };

        Assert.Empty(ImportLinkStarts.Find(Plan(Row(new YearMonth(2005, 1))), contract, [meter], [link, foreign]));
    }

    [Fact]
    public void An_empty_plan_moves_nothing()
    {
        var (contract, meter, link) = Setup(new DateOnly(2004, 5, 1), new DateOnly(2004, 5, 1), new DateOnly(2026, 10, 29));

        Assert.Empty(ImportLinkStarts.Find(Plan(), contract, [meter], [link]));
    }

    private static (Contract Contract, Meter Meter, ContractMeter Link) Setup(DateOnly installedOn, DateOnly contractFrom, DateOnly linkFrom)
    {
        var contract = new Contract { Title = "Water", ValidFrom = contractFrom };
        var meter = new Meter { InstalledOn = installedOn };
        return (contract, meter, new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = linkFrom });
    }

    private static ImportPlan Plan(params PlannedRow[] rows) => new(rows, []);

    private static PlannedRow Row(YearMonth period, DateOnly? readingDate = null)
    {
        var row = new PlannedRow { RowNumber = 2, Period = period, Bill = new Bill() };
        if (readingDate is { } date)
        {
            row.Readings.Add(new PlannedReading(Guid.NewGuid(), new MeterReading { Date = date, Value = 1m }));
        }

        return row;
    }
}
