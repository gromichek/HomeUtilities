using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;

namespace HomeUtilities.Import.Tests;

public class ImportPeriodDefaultsTests
{
    [Theory]
    [InlineData(ServiceKind.ColdWater, 25, 28, ImportPeriodRule.ReadingWindow, false)]
    [InlineData(ServiceKind.HotWater, 25, 28, ImportPeriodRule.ReadingWindow, false)]
    [InlineData(ServiceKind.ColdWater, null, null, ImportPeriodRule.MonthOfDate, false)]
    [InlineData(ServiceKind.Heating, null, null, ImportPeriodRule.PreviousMonth, false)]
    [InlineData(ServiceKind.Electricity, 20, 26, ImportPeriodRule.PreviousMonth, false)]
    [InlineData(ServiceKind.Maintenance, null, null, ImportPeriodRule.PreviousMonth, true)]
    [InlineData(ServiceKind.Waste, null, null, ImportPeriodRule.PreviousMonth, true)]
    [InlineData(ServiceKind.Internet, null, null, ImportPeriodRule.PreviousMonth, true)]
    public void The_suggestion_follows_the_kind_of_service(ServiceKind service, int? from, int? to, ImportPeriodRule rule, bool paymentDate)
    {
        var contract = new Contract { Service = service, ReadingDayFrom = from, ReadingDayTo = to };

        Assert.Equal(new ImportPeriodSuggestion(rule, paymentDate), ImportPeriodDefaults.For(contract));
    }

    [Fact]
    public void The_header_texts_and_column_letters_of_the_mapped_fields_are_recorded()
    {
        var register = Guid.NewGuid();
        var sheet = new SheetData(
            "Water",
            [new SheetRow(1, [SheetCell.String("Date"), SheetCell.String("Cold m3"), SheetCell.String(" Total "), SheetCell.String("Calc")])],
            4);
        var profile = new ImportProfile
        {
            PeriodColumn = 1,
            TotalColumn = 3,
            TotalFallbackColumn = 4,
            Registers = [new RegisterColumnMapping { RegisterId = register, ReadingColumn = 2 }],
        };

        ImportColumnHeaders.Record(profile, sheet);

        Assert.Equal(
            [("Period", "A", "Date"), ("Total", "C", "Total"), ("TotalFallback", "D", "Calc"), ($"Reading:{register}", "B", "Cold m3")],
            profile.ColumnHeaders.Select(h => (h.Field, h.Column, h.Header)));
    }
}
