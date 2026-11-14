using HomeUtilities.Core.Common;
using HomeUtilities.Core.PdfBills;

namespace HomeUtilities.Core.Tests.PdfBills;

/// <summary>T-056: every remaining bill field, with invented texts.</summary>
public class PdfBillAllFieldsTests
{
    private static PdfBillLayout Layout(params PdfBillFieldPattern[] fields) =>
        new() { Name = "Example", Markers = ["EXAMPLE"], Fields = [.. fields] };

    private static PdfBillResult Read(string text, params PdfBillFieldPattern[] fields) =>
        PdfBillReader.Read("EXAMPLE\n" + text, Layout(fields));

    // ---- combine: sum ----------------------------------------------------------------------------------------------

    [Fact]
    public void Consumption_WithSum_AddsTheMatchesOfTwoLines()
    {
        // 6.420 + 0.310 = 6.730
        var result = Read(
            "Hot water declared 6.420 m3\nHot water difference 0.310 m3\n",
            new PdfBillFieldPattern { Field = PdfBillField.Consumption, Pattern = @"Hot water \w+ ([\d.]+) m3", Combine = PdfCombine.Sum });

        Assert.Equal(6.730m, result.Values.Consumption);
        Assert.Contains(PdfBillField.Consumption, result.Found);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Consumption_WithFirst_TakesOnlyTheFirstMatch()
    {
        var result = Read(
            "Hot water declared 6.420 m3\nHot water difference 0.310 m3\n",
            new PdfBillFieldPattern { Field = PdfBillField.Consumption, Pattern = @"Hot water \w+ ([\d.]+) m3" });

        Assert.Equal(6.420m, result.Values.Consumption);
    }

    [Fact]
    public void Sum_WithAnUnparsableMatch_IsNotFound_WithAWarning()
    {
        var result = Read(
            "Item 6.420 m3\nItem 1.2.3.4 m3\n",
            new PdfBillFieldPattern { Field = PdfBillField.Consumption, Pattern = @"Item ([\d.]+) m3", Combine = PdfCombine.Sum });

        Assert.Null(result.Values.Consumption);
        Assert.Contains(PdfBillField.Consumption, result.NotFound);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(PdfBillWarningKind.Unparsable, warning.Kind);
        Assert.Equal("1.2.3.4", warning.Raw);
    }

    [Fact]
    public void ASumLoop_ThatTakesTooLong_TimesOut()
    {
        var text = "EXAMPLE\n" + new string('a', 40) + "!";
        var result = PdfBillReader.Read(
            text,
            Layout(new PdfBillFieldPattern { Field = PdfBillField.Consumption, Pattern = @"(a+)+$", Combine = PdfCombine.Sum }));

        Assert.Null(result.Values.Consumption);
        Assert.Contains(result.Warnings, w => w is { Field: PdfBillField.Consumption, Kind: PdfBillWarningKind.Timeout });
    }

    // ---- tariff lines ----------------------------------------------------------------------------------------------

    [Fact]
    public void Quantities_AreReadPerComponent_AndNamedInTheResult()
    {
        var result = Read(
            "Heat 12.345 MWh\nWater losses 3.5 m3\nHeat amount 780.10\n",
            new PdfBillFieldPattern { Field = PdfBillField.Quantity, Component = "Heat", Pattern = @"^Heat ([\d.]+) MWh" },
            new PdfBillFieldPattern { Field = PdfBillField.Quantity, Component = "Water losses", Pattern = @"Water losses ([\d.]+) m3" },
            new PdfBillFieldPattern { Field = PdfBillField.ComponentAmount, Component = "Heat", Pattern = @"Heat amount ([\d.]+)" },
            new PdfBillFieldPattern { Field = PdfBillField.Quantity, Component = "Missing", Pattern = @"Nothing ([\d.]+)" });

        Assert.Equal([new PdfBillNamedNumber("Heat", 12.345m), new PdfBillNamedNumber("Water losses", 3.5m)], result.Values.Quantities);
        Assert.Equal([new PdfBillNamedNumber("Heat", 780.10m)], result.Values.ComponentAmounts);
        Assert.Equal([new PdfBillNamedEntry(PdfBillField.Quantity, "Missing")], result.NotFoundNamed);
        Assert.Equal(3, result.FoundNamed.Count);
        Assert.DoesNotContain(PdfBillField.Quantity, result.NotFound);
    }

    [Fact]
    public void ANamedEntry_IsNamedByItsComponent_InAWarning()
    {
        var result = Read(
            "Heat x.y MWh\n",
            new PdfBillFieldPattern { Field = PdfBillField.Quantity, Component = "Heat", Pattern = @"Heat ([\w.]+) MWh" });

        var warning = Assert.Single(result.Warnings);
        Assert.Equal("Heat", warning.Subject);
        Assert.Equal("x.y", warning.Raw);
    }

    // ---- notes -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Notes_WithAllAndFormat_JoinsTheFormattedMatches()
    {
        var result = Read(
            "1111111111  105.20 - 106.10\n2222222222  8200.00 - 8315.40\n",
            new PdfBillFieldPattern
            {
                Field = PdfBillField.Notes,
                Pattern = @"^(?<m>\d{10})\s+(?<a>[\d.]+) - (?<b>[\d.]+)$",
                Combine = PdfCombine.All,
                Format = "${m}: ${a} – ${b}",
            });

        Assert.Equal("1111111111: 105.20 – 106.10; 2222222222: 8200.00 – 8315.40", result.Values.Notes);
    }

    [Fact]
    public void Notes_WithoutCombine_TakesTheFirstValue()
    {
        var result = Read("Remark: pay by 10th\nRemark: second\n", new PdfBillFieldPattern { Field = PdfBillField.Notes, Pattern = @"Remark: (.+)" });

        Assert.Equal("pay by 10th", result.Values.Notes);
    }

    // ---- net, service period, title --------------------------------------------------------------------------------

    [Fact]
    public void NetTitleAndServicePeriod_AreRead()
    {
        var result = Read(
            "Service 01.10.2026 - 31.10.2026\nNet 54.00\nTitle: Water October\n",
            new PdfBillFieldPattern { Field = PdfBillField.Net, Pattern = @"Net ([\d.]+)" },
            new PdfBillFieldPattern { Field = PdfBillField.ServiceFrom, Pattern = @"Service (\d{2}\.\d{2}\.\d{4}) - " },
            new PdfBillFieldPattern { Field = PdfBillField.ServiceTo, Pattern = @" - (\d{2}\.\d{2}\.\d{4})" },
            new PdfBillFieldPattern { Field = PdfBillField.Title, Pattern = @"Title: (.+)" });

        Assert.Equal(54.00m, result.Values.Net);
        Assert.Equal(new DateOnly(2026, 10, 1), result.Values.ServiceFrom);
        Assert.Equal(new DateOnly(2026, 10, 31), result.Values.ServiceTo);
        Assert.Equal("Water October", result.Values.Title);
    }

    [Fact]
    public void AnUndefinedNewField_IsNotListedAsNotFound()
    {
        var result = Read("Net 54.00\n", new PdfBillFieldPattern { Field = PdfBillField.Net, Pattern = @"Net ([\d.]+)" });

        Assert.DoesNotContain(PdfBillField.Title, result.NotFound);
        Assert.DoesNotContain(PdfBillField.Notes, result.NotFound);
    }

    [Fact]
    public void ADefinedField_WithoutAMatch_IsNotFound()
    {
        var result = Read("nothing\n", new PdfBillFieldPattern { Field = PdfBillField.Title, Pattern = @"Title: (.+)" });

        Assert.Contains(PdfBillField.Title, result.NotFound);
    }

    [Fact]
    public void TotalNetAndVat_ThatDoNotAddUp_GiveAWarning()
    {
        // 54.00 + 6.43 = 60.43 against 60.48: 0.05 apart
        var result = Read(
            "Total 60.48\nNet 54.00\nVAT 6.43\n",
            new PdfBillFieldPattern { Field = PdfBillField.Gross, Pattern = @"Total ([\d.]+)" },
            new PdfBillFieldPattern { Field = PdfBillField.Net, Pattern = @"Net ([\d.]+)" },
            new PdfBillFieldPattern { Field = PdfBillField.Vat, Pattern = @"VAT ([\d.]+)" });

        Assert.Contains(result.Warnings, w => w.Kind == PdfBillWarningKind.AmountsMismatch);
    }

    [Fact]
    public void TotalNetAndVat_WithinACent_GiveNoWarning()
    {
        var result = Read(
            "Total 60.48\nNet 54.00\nVAT 6.47\n",
            new PdfBillFieldPattern { Field = PdfBillField.Gross, Pattern = @"Total ([\d.]+)" },
            new PdfBillFieldPattern { Field = PdfBillField.Net, Pattern = @"Net ([\d.]+)" },
            new PdfBillFieldPattern { Field = PdfBillField.Vat, Pattern = @"VAT ([\d.]+)" });

        Assert.Empty(result.Warnings);
    }

    // ---- amounts ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Amounts_WithTotalAndNet_DeriveTheVat()
    {
        // 60.48 − 54.00 = 6.48 (12 %)
        var amounts = PdfBillAmounts.Resolve(new PdfBillValues { Gross = 60.48m, Net = 54.00m });

        Assert.NotNull(amounts);
        Assert.Equal(54.00m, amounts.Net);
        Assert.Equal(6.48m, amounts.Vat);
        Assert.Equal(60.48m, amounts.Gross);
        Assert.Equal(12m, amounts.VatPercent);
    }

    [Fact]
    public void Amounts_WithTotalNetAndVat_KeepTheTotal_AndNetPlusVatEqualsTotal()
    {
        // printed 54.00 + 6.43 ≠ 60.48; the total wins, the VAT amount is kept → net 54.05
        var amounts = PdfBillAmounts.Resolve(new PdfBillValues { Gross = 60.48m, Net = 54.00m, Vat = 6.43m });

        Assert.NotNull(amounts);
        Assert.Equal(60.48m, amounts.Gross);
        Assert.Equal(amounts.Gross, amounts.Net + amounts.Vat);
    }

    [Fact]
    public void Amounts_WithNetAndPrintedRate_KeepThePrintedRate()
    {
        var amounts = PdfBillAmounts.Resolve(new PdfBillValues { Gross = 60.48m, Net = 54.00m, VatPercent = 12m });

        Assert.Equal(12m, amounts!.VatPercent);
    }

    [Fact]
    public void Amounts_WithoutATotal_AreDerivedFromNetAndVat()
    {
        var amounts = PdfBillAmounts.Resolve(new PdfBillValues { Net = 54.00m, Vat = 6.48m });

        Assert.Equal(60.48m, amounts!.Gross);
    }

    [Fact]
    public void Amounts_WithNetOnly_AreNull()
    {
        Assert.Null(PdfBillAmounts.Resolve(new PdfBillValues { Net = 54.00m }));
    }

    // ---- custom fields ---------------------------------------------------------------------------------------------

    [Fact]
    public void CustomFields_AreReadByName_AsRawText()
    {
        var result = Read(
            "Account no 778899\nAdvance 12.50\nReceipt 05.10.2026\nPart A 1.5\nPart B 2.25\n",
            new PdfBillFieldPattern { Field = PdfBillField.CustomField, Name = "Account", Pattern = @"Account no (\d+)" },
            new PdfBillFieldPattern { Field = PdfBillField.CustomField, Name = "Advance", Pattern = @"Advance ([\d.]+)" },
            new PdfBillFieldPattern { Field = PdfBillField.CustomField, Name = "Received", Pattern = @"Receipt (\S+)" },
            new PdfBillFieldPattern { Field = PdfBillField.CustomField, Name = "Parts", Pattern = @"Part \w ([\d.]+)", Combine = PdfCombine.Sum },
            new PdfBillFieldPattern { Field = PdfBillField.CustomField, Name = "Unknown", Pattern = @"Nothing (\d+)" });

        Assert.Equal(
            [
                new PdfBillCustomValue("Account", "778899"),
                new PdfBillCustomValue("Advance", "12.50"),
                new PdfBillCustomValue("Received", "05.10.2026"),
                new PdfBillCustomValue("Parts", "3.75", 3.75m),
            ],
            result.Values.CustomFields);
        Assert.Equal([new PdfBillNamedEntry(PdfBillField.CustomField, "Unknown")], result.NotFoundNamed);
    }

    // ---- lines -----------------------------------------------------------------------------------------------------

    private const string LinesPattern =
        @"^(?<description>[A-Za-z ]+?)\s+(?<quantity>[\d.]+)\s+(?<unit>m3|kWh)\s+(?<price>[\d.]+)\s+(?<net>[\d.]+)\s+(?<vat>[\d.]+)$";

    [Fact]
    public void Lines_GiveOneLinePerTableRow()
    {
        var result = Read(
            "Cold water   5.000 m3   1.410   7.05   1.48\nSewage   5.000 m3   1.200   6.00   1.26\n",
            new PdfBillFieldPattern { Field = PdfBillField.Lines, Pattern = LinesPattern, Combine = PdfCombine.All });

        Assert.Equal(
            [
                new PdfBillLineValue("Cold water", 5.000m, "m3", 1.410m, 7.05m, 1.48m),
                new PdfBillLineValue("Sewage", 5.000m, "m3", 1.200m, 6.00m, 1.26m),
            ],
            result.Values.Lines);
        Assert.Contains(PdfBillField.Lines, result.Found);
    }

    [Fact]
    public void ALineWithAnUnparsableNumber_IsSkipped_WithAWarning()
    {
        var result = Read(
            "Cold water   5.000 m3   1.410   7.05   1.48\nSewage   5.0.0 m3   1.200   6.00   1.26\n",
            new PdfBillFieldPattern { Field = PdfBillField.Lines, Pattern = LinesPattern, Combine = PdfCombine.All });

        Assert.Equal("Cold water", Assert.Single(result.Values.Lines).Description);
        Assert.Contains(result.Warnings, w => w is { Field: PdfBillField.Lines, Kind: PdfBillWarningKind.Unparsable });
    }

    [Fact]
    public void MissingLineGroups_StayEmpty()
    {
        var result = Read(
            "Standing charge 2.50\n",
            new PdfBillFieldPattern { Field = PdfBillField.Lines, Pattern = @"(?<description>[A-Za-z ]+?)\s+(?<net>[\d.]+)$", Combine = PdfCombine.All });

        Assert.Equal([new PdfBillLineValue("Standing charge", null, null, null, 2.50m, null)], result.Values.Lines);
    }

    // ---- layout file -----------------------------------------------------------------------------------------------

    [Fact]
    public void TheNewProperties_RoundTripThroughTheFile()
    {
        var layout = Layout(
            new PdfBillFieldPattern { Field = PdfBillField.Quantity, Component = "Heat", Pattern = @"Heat ([\d.]+)", Combine = PdfCombine.Sum },
            new PdfBillFieldPattern { Field = PdfBillField.CustomField, Name = "Account", Pattern = @"(\d+)" },
            new PdfBillFieldPattern { Field = PdfBillField.Notes, Pattern = @"(?<m>\d+)", Combine = PdfCombine.All, Format = "${m}" },
            new PdfBillFieldPattern { Field = PdfBillField.Number, Pattern = @"(\S+)" });

        var json = PdfBillLayoutFile.Write([layout]);
        var read = PdfBillLayoutFile.Read(json);

        Assert.True(read.Succeeded);
        var fields = read.Layouts[0].Fields;
        Assert.Equal("Heat", fields[0].Component);
        Assert.Equal(PdfCombine.Sum, fields[0].Combine);
        Assert.Equal("Account", fields[1].Name);
        Assert.Equal(PdfCombine.All, fields[2].Combine);
        Assert.Equal("${m}", fields[2].Format);
        Assert.Equal(PdfCombine.First, fields[3].Combine);
        Assert.Contains("\"combine\": \"sum\"", json);
    }

    [Fact]
    public void DefaultProperties_AreOmittedFromTheFile()
    {
        var json = PdfBillLayoutFile.Write([Layout(new PdfBillFieldPattern { Field = PdfBillField.Number, Pattern = @"(\S+)" })]);

        Assert.DoesNotContain("component", json);
        Assert.DoesNotContain("combine", json);
        Assert.DoesNotContain("format", json);
        Assert.DoesNotContain("\"name\": null", json);
    }

    [Fact]
    public void AnOldFile_WithoutTheNewProperties_ReadsUnchanged()
    {
        const string json = """
            { "version": 1, "layouts": [ { "name": "Old", "markers": ["X"], "fields": [ { "field": "Number", "pattern": "(\\S+)" } ] } ] }
            """;

        var read = PdfBillLayoutFile.Read(json);

        Assert.True(read.Succeeded);
        var field = Assert.Single(read.Layouts[0].Fields);
        Assert.Equal(PdfCombine.First, field.Combine);
        Assert.Null(field.Component);
    }

    public static TheoryData<PdfBillFieldPattern, string> InvalidEntries => new()
    {
        { new() { Field = PdfBillField.Quantity, Pattern = "(x)" }, ErrorCodes.PdfLayoutComponentRequired },
        { new() { Field = PdfBillField.ComponentAmount, Pattern = "(x)", Component = " " }, ErrorCodes.PdfLayoutComponentRequired },
        { new() { Field = PdfBillField.CustomField, Pattern = "(x)" }, ErrorCodes.PdfLayoutFieldNameRequired },
        { new() { Field = PdfBillField.Number, Pattern = "(x)", Combine = PdfCombine.All }, ErrorCodes.PdfLayoutCombineInvalid },
        { new() { Field = PdfBillField.Title, Pattern = "(x)", Combine = PdfCombine.Sum }, ErrorCodes.PdfLayoutCombineInvalid },
        { new() { Field = PdfBillField.IssuedOn, Pattern = "(x)", Combine = PdfCombine.Sum }, ErrorCodes.PdfLayoutCombineInvalid },
        { new() { Field = PdfBillField.Notes, Pattern = "(x)", Combine = PdfCombine.Sum }, ErrorCodes.PdfLayoutCombineInvalid },
        { new() { Field = PdfBillField.Quantity, Component = "A", Pattern = "(x)", Combine = PdfCombine.All }, ErrorCodes.PdfLayoutCombineInvalid },
        { new() { Field = PdfBillField.Gross, Pattern = "(x)", Format = "${v}" }, ErrorCodes.PdfLayoutFormatInvalid },
        { new() { Field = PdfBillField.Lines, Pattern = "(x)", Combine = PdfCombine.All }, ErrorCodes.PdfLayoutLinesDescriptionRequired },
    };

    [Theory]
    [MemberData(nameof(InvalidEntries))]
    public void Validation_ReportsTheEntry(PdfBillFieldPattern entry, string code)
    {
        var errors = PdfBillLayoutFile.Validate(Layout(entry));

        Assert.Contains(errors, e => e.Code == code);
    }

    [Fact]
    public void AnUnknownCombineWord_IsReportedOnImport()
    {
        const string json = """
            { "version": 1, "layouts": [ { "name": "L", "markers": ["X"], "fields": [ { "field": "Consumption", "pattern": "(\\d+)", "combine": "total" } ] } ] }
            """;

        var read = PdfBillLayoutFile.Read(json);

        Assert.False(read.Succeeded);
        Assert.Contains(read.Errors, e => e.Code == ErrorCodes.PdfLayoutCombineInvalid);
    }

    [Fact]
    public void ValidEntries_PassValidation()
    {
        var errors = PdfBillLayoutFile.Validate(Layout(
            new PdfBillFieldPattern { Field = PdfBillField.Quantity, Component = "Heat", Pattern = "(x)", Combine = PdfCombine.Sum },
            new PdfBillFieldPattern { Field = PdfBillField.CustomField, Name = "A", Pattern = "(x)", Combine = PdfCombine.Sum },
            new PdfBillFieldPattern { Field = PdfBillField.Notes, Pattern = "(?<a>x)", Combine = PdfCombine.All, Format = "${a}" },
            new PdfBillFieldPattern { Field = PdfBillField.Lines, Pattern = "(?<description>x)", Combine = PdfCombine.All }));

        Assert.Empty(errors);
    }
}
