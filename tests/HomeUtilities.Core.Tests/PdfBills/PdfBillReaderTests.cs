using HomeUtilities.Core.Common;
using HomeUtilities.Core.PdfBills;

namespace HomeUtilities.Core.Tests.PdfBills;

public class PdfBillReaderTests
{
    private const string WaterText =
        "SIA \"Piemērs Ūdens\"                         RĒĶINS NR.: UD-2026-0457\n" +
        "Datums:  06.11.2026\n" +
        "Periods:  2026. gada oktobris\n" +
        "APMAKSAS TERMIŅŠ   20.11.2026.\n" +
        "PVN 21 %   10,33\n" +
        "KOPĀ APMAKSAI EUR:   59,49\n";

    private static PdfBillLayout Water() => new()
    {
        Name = "Example Water",
        Markers = ["SIA \"Piemērs Ūdens\"", "RĒĶINS NR."],
        Fields =
        [
            new() { Field = PdfBillField.Number, Pattern = @"RĒĶINS NR\.:\s+(\S+)" },
            new() { Field = PdfBillField.IssuedOn, Pattern = @"Datums:\s+(\d{2}\.\d{2}\.\d{4})" },
            new() { Field = PdfBillField.Period, Pattern = @"Periods:\s+(\d{4}\. gada \p{L}+)" },
            new() { Field = PdfBillField.DueOn, Pattern = @"APMAKSAS TERMIŅŠ\s+(\d{2}\.\d{2}\.\d{4}\.?)" },
            new() { Field = PdfBillField.Vat, Pattern = @"PVN \d+ %\s+(?<v>[\d,]+)" },
            new() { Field = PdfBillField.VatPercent, Pattern = @"PVN (\d+) %" },
            new() { Field = PdfBillField.Gross, Pattern = @"KOPĀ APMAKSAI EUR:\s+([\d.,]+)" },
        ],
    };

    [Fact]
    public void AllFieldsAreFound_WithTheFittingLayout()
    {
        var result = PdfBillReader.Read(WaterText, [Water()]);

        Assert.True(result.LayoutFound);
        Assert.Equal("Example Water", result.LayoutName);
        Assert.Equal("UD-2026-0457", result.Values.Number);
        Assert.Equal(new DateOnly(2026, 11, 6), result.Values.IssuedOn);
        Assert.Equal(new YearMonth(2026, 10), result.Values.Period);
        Assert.Equal(new DateOnly(2026, 11, 20), result.Values.DueOn);
        Assert.Equal(10.33m, result.Values.Vat);
        Assert.Equal(21m, result.Values.VatPercent);
        Assert.Equal(59.49m, result.Values.Gross);
        Assert.Empty(result.NotFound);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void TheLayoutWithMoreMarkers_Wins()
    {
        var general = new PdfBillLayout { Name = "General", Markers = ["RĒĶINS NR."], Fields = [new() { Field = PdfBillField.Number, Pattern = "NR\\.:\\s+(\\S+)" }] };

        var result = PdfBillReader.Read(WaterText, [general, Water()]);

        Assert.Equal("Example Water", result.LayoutName);
    }

    [Fact]
    public void MarkersAreCaseInsensitive_AndAllMustOccur()
    {
        var lower = new PdfBillLayout { Name = "Lower", Markers = ["rēķins nr."] };
        var missing = new PdfBillLayout { Name = "Missing", Markers = ["RĒĶINS NR.", "Heating"] };

        Assert.Equal("Lower", PdfBillReader.Read(WaterText, [missing, lower]).LayoutName);
        Assert.False(PdfBillReader.Read(WaterText, [missing]).LayoutFound);
    }

    [Fact]
    public void WithoutAFittingLayout_NothingIsFound()
    {
        var result = PdfBillReader.Read("Some other bill", [Water()]);

        Assert.False(result.LayoutFound);
        Assert.Null(result.LayoutName);
        Assert.Null(result.Values.Gross);
    }

    [Fact]
    public void ALayoutWithoutMarkers_NeverFits() =>
        Assert.False(PdfBillReader.Read(WaterText, [new PdfBillLayout { Name = "Empty" }]).LayoutFound);

    [Fact]
    public void ADeletedLayout_IsNotUsed()
    {
        var layout = Water();
        layout.IsDeleted = true;

        Assert.False(PdfBillReader.Read(WaterText, [layout]).LayoutFound);
    }

    [Fact]
    public void AFieldWithoutAMatch_IsNotFound()
    {
        var layout = Water();
        layout.Fields.RemoveAll(f => f.Field == PdfBillField.Vat);
        layout.Fields.First(f => f.Field == PdfBillField.DueOn).Pattern = "DUE DATE\\s+(\\S+)";

        var result = PdfBillReader.Read(WaterText, layout);

        Assert.Contains(PdfBillField.Vat, result.NotFound);
        Assert.Contains(PdfBillField.DueOn, result.NotFound);
        Assert.Null(result.Values.DueOn);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void AFoundValueThatDoesNotParse_IsNotFound_WithAWarningHoldingTheRawText()
    {
        var layout = Water();
        layout.Fields.First(f => f.Field == PdfBillField.Gross).Pattern = @"Datums:\s+(\S+)";
        layout.Fields.First(f => f.Field == PdfBillField.IssuedOn).Pattern = @"RĒĶINS NR\.:\s+(\S+)";

        var result = PdfBillReader.Read(WaterText, layout);

        Assert.Null(result.Values.Gross);
        Assert.Null(result.Values.IssuedOn);
        Assert.Contains(PdfBillField.Gross, result.NotFound);
        Assert.Contains(result.Warnings, w => w.Field == PdfBillField.Gross && w.Kind == PdfBillWarningKind.Unparsable && w.Raw == "06.11.2026");
        Assert.Contains(result.Warnings, w => w.Field == PdfBillField.IssuedOn && w.Raw == "UD-2026-0457");
    }

    [Fact]
    public void TheNamedGroupV_IsUsedBeforeGroupOne()
    {
        var layout = new PdfBillLayout
        {
            Name = "Groups",
            Markers = ["RĒĶINS"],
            Fields = [new() { Field = PdfBillField.Number, Pattern = @"(RĒĶINS) NR\.:\s+(?<v>\S+)" }],
        };

        Assert.Equal("UD-2026-0457", PdfBillReader.Read(WaterText, layout).Values.Number);
    }

    [Fact]
    public void WithoutGroups_TheWholeMatchIsTheValue()
    {
        var layout = new PdfBillLayout
        {
            Name = "Whole",
            Markers = ["RĒĶINS"],
            Fields = [new() { Field = PdfBillField.Number, Pattern = @"UD-\d{4}-\d+" }],
        };

        Assert.Equal("UD-2026-0457", PdfBillReader.Read(WaterText, layout).Values.Number);
    }

    [Fact]
    public void TheFirstMatchWins()
    {
        var layout = new PdfBillLayout
        {
            Name = "First",
            Markers = ["x"],
            Fields = [new() { Field = PdfBillField.Gross, Pattern = @"EUR:\s+([\d,]+)" }],
        };

        Assert.Equal(1m, PdfBillReader.Read("x EUR: 1,00\nEUR: 2,00", layout).Values.Gross);
    }

    [Fact]
    public void PatternsRunOnTheWholeText_WithMultilineAnchors()
    {
        var layout = new PdfBillLayout
        {
            Name = "Anchors",
            Markers = ["x"],
            Fields = [new() { Field = PdfBillField.Number, Pattern = @"^No\. (\w+)$" }],
        };

        Assert.Equal("77", PdfBillReader.Read("x\nNo. 77\nend", layout).Values.Number);
    }

    [Fact]
    public void ARunawayPattern_TimesOut_IntoAWarning_NotACrash()
    {
        var layout = new PdfBillLayout
        {
            Name = "Slow",
            Markers = ["x"],
            Fields = [new() { Field = PdfBillField.Number, Pattern = @"(a+)+$" }],
        };
        var text = "x " + new string('a', 60) + "!";

        var result = PdfBillReader.Read(text, layout);

        Assert.Contains(PdfBillField.Number, result.NotFound);
        Assert.Contains(result.Warnings, w => w.Field == PdfBillField.Number && w.Kind == PdfBillWarningKind.Timeout);
    }

    [Fact]
    public void AnInvalidStoredPattern_FindsNothing_AndDoesNotThrow()
    {
        var layout = new PdfBillLayout { Name = "Broken", Markers = ["x"], Fields = [new() { Field = PdfBillField.Number, Pattern = "(" }] };

        var result = PdfBillReader.Read("x", layout);

        Assert.Contains(PdfBillField.Number, result.NotFound);
    }
}
