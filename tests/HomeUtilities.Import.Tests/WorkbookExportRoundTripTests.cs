using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;

namespace HomeUtilities.Import.Tests;

/// <summary>Export the invented water data, import the file again into an empty database with the stored profile: the same records come back.</summary>
public class WorkbookExportRoundTripTests
{
    private static ImportPlan RoundTrip(ExportScenario scenario)
    {
        using var stream = new MemoryStream();
        new ClosedXmlExportWriter().Write(scenario.Export(), stream);

        stream.Position = 0;
        var sheet = new ClosedXmlWorkbookReader().ReadSheet(stream, "Aqua Water");
        return ImportPlanner.Plan(scenario.Profile, sheet, new ImportContext(scenario.Contract, scenario.EmptyMeters(), []));
    }

    [Theory]
    [InlineData(ImportPeriodRule.PreviousMonth)]
    [InlineData(ImportPeriodRule.ReadingWindow)]
    [InlineData(ImportPeriodRule.MonthOfDate)]
    public void The_import_of_the_export_restores_bills_payments_readings_and_the_replacement(ImportPeriodRule rule)
    {
        var scenario = new ExportScenario(rule);

        var plan = RoundTrip(scenario);

        Assert.DoesNotContain(plan.Rows, r => r.HasErrors);

        // Bills: period, total, currency (December 2013 is LVL), the note without the sheet's header repeated.
        var bills = plan.Rows.Where(r => r.Bill is not null).Select(r => r.Bill!).OrderBy(b => b.Period).ToList();
        Assert.Equal(scenario.Bills.OrderBy(b => b.Period).Select(b => (b.Period, b.Gross, b.Currency)), bills.Select(b => (b.Period, b.Gross, b.Currency)));
        Assert.Equal("Comment: tariff changed", bills[0].Notes);

        // Payments: December and January were paid in full.
        var payments = plan.Rows.Where(r => r.Payment is not null).Select(r => (r.Bill!.Period, r.Payment!.Amount)).OrderBy(p => p.Period).ToList();
        Assert.Equal([(new YearMonth(2013, 12), 12.34m), (new YearMonth(2014, 1), 13.00m)], payments);

        // Readings: the dial values of the hot meter and of the cold meters (the old one up to the replacement, the new one after it).
        var readings = plan.Rows.SelectMany(r => r.Readings).ToList();
        var hot = readings.Where(r => r.RegisterId == scenario.Hot.Registers[0].Id).OrderBy(r => r.Reading.Date).Select(r => r.Reading.Value);
        Assert.Equal([50m, 53m, 56m, 59m, 62m], hot);
        var oldCold = readings.Where(r => r.RegisterId == scenario.Cold.Registers[0].Id).OrderBy(r => r.Reading.Date).Select(r => r.Reading.Value);
        Assert.Equal([100m, 105m, 110m], oldCold);

        // The replacement: found again from the extra row and its note, with the serial number, the final and the initial reading.
        var replacement = Assert.Single(plan.Replacements);
        Assert.Equal(new DateOnly(2014, 3, 15), replacement.Date);
        Assert.Equal(scenario.Cold.Id, replacement.OldMeterId);
        Assert.Equal("CW-20140", replacement.NewMeter.SerialNumber);
        var line = Assert.Single(replacement.Registers);
        Assert.Equal((110m, 0m), (line.FinalReading.Value, line.InitialReading.Value));

        var newCold = readings.Where(r => r.RegisterId == replacement.NewMeter.Registers[0].Id).OrderBy(r => r.Reading.Date).Select(r => r.Reading.Value);
        Assert.Equal([0m, 6m, 12m], newCold);
    }
}
