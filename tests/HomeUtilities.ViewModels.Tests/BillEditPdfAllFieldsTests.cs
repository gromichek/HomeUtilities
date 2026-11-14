using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.PdfBills;
using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>"Fill from PDF" beyond the header (T-056): quantities, notes, net, service period, custom fields, lines. Invented texts.</summary>
public class BillEditPdfAllFieldsTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 27);

    private const string Text =
        "EXAMPLE HEAT SUPPLY\n" +
        "Period: 2026-11\n" +
        "Total 60.48\n" +
        "Net 54.00\n" +
        "Hot water declared 6.420 m3\n" +
        "Hot water difference 0.310 m3\n" +
        "Heat 12.345 MWh\n" +
        "Heat amount 864.15\n" +
        "Water losses 3.5 m3\n" +
        "1111111111  105.20 - 106.10\n" +
        "2222222222  8200.00 - 8315.40\n" +
        "Service 01.11.2026 - 30.11.2026\n" +
        "Title: Heating November\n";

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly BillTestKit _kit = new(Today);

    public BillEditPdfAllFieldsTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private static PdfBillFieldPattern P(PdfBillField field, string pattern, string? component = null, string? name = null, PdfCombine combine = PdfCombine.First, string? format = null) =>
        new() { Field = field, Pattern = pattern, Component = component, Name = name, Combine = combine, Format = format };

    private TariffComponent AddFromBill(Contract contract, string name, MeterUnit unit, int order, DateOnly? validFrom = null)
    {
        var component = new TariffComponent
        {
            ContractId = contract.Id,
            Name = name,
            ChargeKind = ServiceKind.Heating,
            Basis = ChargeBasis.QuantityFromBill,
            QuantityUnit = unit,
            VatCode = "LV-STANDARD",
            SortOrder = order,
        };
        component.Rates.Add(new TariffRate { ComponentId = component.Id, ValidFrom = validFrom ?? new DateOnly(2020, 1, 1), Price = 70m, Currency = Currency.EUR });
        _kit.Kit.Tariffs.Stored.Add(component);
        return component;
    }

    private async Task<BillEditViewModel> EditorAsync(Contract contract, string text, params PdfBillFieldPattern[] fields)
    {
        _kit.PdfLayouts.Stored.Add(new PdfBillLayout { Name = "Example Heat", Markers = ["EXAMPLE HEAT SUPPLY"], Fields = [.. fields] });
        _kit.PdfReader.Text = text;
        _kit.Kit.Media.Next = FakeMediaService.Photo("heat-nov.pdf", "application/pdf", 1, 2, 3);
        var vm = _kit.CreateEditor();
        await vm.LoadAsync(null);
        _kit.Kit.Dialogs.ChooseScript = options => options.Contains(contract.Title) ? contract.Title : null;
        await vm.ChooseContractCommand.ExecuteAsync(null);
        return vm;
    }

    private string LastAlert() => _kit.Kit.Dialogs.Alerts[^1].Message;

    private static decimal Number(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);

    [Fact]
    public async Task ConsumptionAndQuantities_AreSet_FoundCaseInsensitivelyByName()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        AddFromBill(contract, "Heat", MeterUnit.MWh, 1);
        AddFromBill(contract, "Water losses", MeterUnit.M3, 2);
        var vm = await EditorAsync(
            contract,
            Text,
            P(PdfBillField.Consumption, @"Hot water \w+ ([\d.]+) m3", combine: PdfCombine.Sum),
            P(PdfBillField.Quantity, @"^Heat ([\d.]+) MWh", component: "  HEAT "),
            P(PdfBillField.Quantity, @"Water losses ([\d.]+) m3", component: "water LOSSES"),
            P(PdfBillField.ComponentAmount, @"Heat amount ([\d.]+)", component: "Heat"));

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        // 6.420 + 0.310 = 6.730
        Assert.Equal(6.730m, Number(vm.ConsumptionText));
        Assert.Equal(12.345m, Number(vm.QuantityRows.Single(r => r.Name == "Heat").Text));
        Assert.Equal(864.15m, Number(vm.QuantityRows.Single(r => r.Name == "Heat").AmountText));
        Assert.Equal(3.5m, Number(vm.QuantityRows.Single(r => r.Name == "Water losses").Text));
        var message = LastAlert();
        Assert.Contains("PdfField_Consumption [en]: 6.73", message);
        Assert.Contains("Heat: 12.345", message);
        Assert.Contains("Water losses: 3.5", message);
        Assert.DoesNotContain("PdfWarning", message);
    }

    [Fact]
    public async Task AnUnknownComponent_GivesAWarning_AndTheOtherValuesAreStillFilled()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        AddFromBill(contract, "Heat", MeterUnit.MWh, 1);
        var vm = await EditorAsync(
            contract,
            Text,
            P(PdfBillField.Consumption, @"Hot water declared ([\d.]+) m3"),
            P(PdfBillField.Quantity, @"^Heat ([\d.]+) MWh", component: "Heat"),
            P(PdfBillField.Quantity, @"Water losses ([\d.]+) m3", component: "Water losses"));

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal(6.420m, Number(vm.ConsumptionText));
        Assert.Equal(12.345m, Number(vm.QuantityRows.Single().Text));
        Assert.Contains("PdfWarningComponentMissing Water losses [en]", LastAlert());
    }

    [Fact]
    public async Task APeriodChange_IsAwaited_BeforeTheQuantitiesAreSet()
    {
        // The component only exists from November; the bill starts in another month, so its row appears with the PDF's period.
        var contract = _kit.AddWaterContract(tariff: false);
        AddFromBill(contract, "Heat", MeterUnit.MWh, 1, validFrom: new DateOnly(2026, 11, 1));
        var vm = await EditorAsync(
            contract,
            Text,
            P(PdfBillField.Period, @"Period: (\d{4}-\d{2})"),
            P(PdfBillField.Quantity, @"^Heat ([\d.]+) MWh", component: "Heat"));

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal(new YearMonth(2026, 11), vm.Period);
        Assert.Equal(12.345m, Number(vm.QuantityRows.Single(r => r.Name == "Heat").Text));
        Assert.DoesNotContain("PdfWarningComponentMissing", LastAlert());
    }

    [Fact]
    public async Task Notes_AreAppendedOnANewLine_AndNotAddedTwice()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await EditorAsync(
            contract,
            Text,
            P(PdfBillField.Notes, @"^(?<m>\d{10})\s+(?<a>[\d.]+) - (?<b>[\d.]+)$", combine: PdfCombine.All, format: "${m}: ${a} – ${b}"));
        vm.Notes = "Paid at the counter";

        await vm.FillFromPdfCommand.ExecuteAsync(null);
        var once = vm.Notes;
        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal("Paid at the counter" + Environment.NewLine + "1111111111: 105.20 – 106.10; 2222222222: 8200.00 – 8315.40", once);
        Assert.Equal(once, vm.Notes);
    }

    [Fact]
    public async Task Notes_WithNoExistingComment_AreJustTheText()
    {
        var vm = await EditorAsync(_kit.AddWaterContract(tariff: false), Text, P(PdfBillField.Notes, @"Title: (.+)"));

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal("Heating November", vm.Notes);
    }

    [Fact]
    public async Task Notes_BeyondTheLimit_AreCut_WithAWarning()
    {
        var vm = await EditorAsync(_kit.AddWaterContract(tariff: false), Text, P(PdfBillField.Notes, @"Title: (.+)"));
        vm.Notes = new string('x', InputLimits.Notes - 3);

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal(InputLimits.Notes, vm.Notes.Length);
        Assert.Contains("PdfWarningNotesTooLong", LastAlert());
    }

    [Fact]
    public async Task Net_WithTheTotal_GivesTheVat()
    {
        // 60.48 − 54.00 = 6.48; 6.48 / 54.00 = 12 %
        var vm = await EditorAsync(
            _kit.AddWaterContract(tariff: false),
            Text,
            P(PdfBillField.Gross, @"^Total ([\d.]+)"),
            P(PdfBillField.Net, @"^Net ([\d.]+)"));

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal("60.48", vm.Amounts.GrossText);
        Assert.Equal("54.00", vm.Amounts.NetText);
        Assert.Equal("6.48", vm.Amounts.VatText);
        Assert.Equal("12", vm.Amounts.VatPercentText);
    }

    [Fact]
    public async Task TotalNetAndVatThatDoNotAddUp_KeepTheTotal_AndWarn()
    {
        var vm = await EditorAsync(
            _kit.AddWaterContract(tariff: false),
            Text + "VAT 6.43\n",
            P(PdfBillField.Gross, @"^Total ([\d.]+)"),
            P(PdfBillField.Net, @"^Net ([\d.]+)"),
            P(PdfBillField.Vat, @"^VAT ([\d.]+)"));

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal("60.48", vm.Amounts.GrossText);
        Assert.Equal(60.48m, Number(vm.Amounts.NetText) + Number(vm.Amounts.VatText));
        Assert.Contains("PdfWarningAmountsMismatch [en]", LastAlert());
    }

    [Fact]
    public async Task NetWithoutATotal_IsSkipped_WithAWarning()
    {
        var vm = await EditorAsync(_kit.AddWaterContract(tariff: false), Text, P(PdfBillField.Net, @"^Net ([\d.]+)"));

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.Amounts.NetText);
        var message = LastAlert();
        Assert.Contains("PdfWarningNetNeedsTotal [en]", message);
        Assert.DoesNotContain("PdfField_Net [en]:", message);
    }

    [Fact]
    public async Task ServicePeriod_IsSet_WhenBothDatesAreFound()
    {
        var vm = await EditorAsync(
            _kit.AddWaterContract(tariff: false),
            Text,
            P(PdfBillField.ServiceFrom, @"Service (\d{2}\.\d{2}\.\d{4}) - "),
            P(PdfBillField.ServiceTo, @" - (\d{2}\.\d{2}\.\d{4})"));

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.True(vm.HasServicePeriod);
        Assert.Equal(new DateOnly(2026, 11, 1), vm.ServiceFrom);
        Assert.Equal(new DateOnly(2026, 11, 30), vm.ServiceTo);
    }

    [Fact]
    public async Task ServicePeriod_WithOnlyOneDate_IsNotSet_WithAWarning()
    {
        var vm = await EditorAsync(
            _kit.AddWaterContract(tariff: false),
            Text,
            P(PdfBillField.ServiceFrom, @"Service (\d{2}\.\d{2}\.\d{4}) - "),
            P(PdfBillField.ServiceTo, @"Ends (\d{2}\.\d{2}\.\d{4})"));

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.False(vm.HasServicePeriod);
        Assert.Contains("PdfWarningServicePeriodPartial [en]", LastAlert());
    }

    [Fact]
    public async Task Title_IsSet()
    {
        var vm = await EditorAsync(_kit.AddWaterContract(tariff: false), Text, P(PdfBillField.Title, @"Title: (.+)"));

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal("Heating November", vm.BillTitle);
    }

    [Fact]
    public async Task CustomFields_AreSetByName_ParsedByTheFieldsKind()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        contract.CustomFields.AddRange(
        [
            new CustomFieldDefinition { OwnerId = contract.Id, Name = "Reference", Kind = CustomFieldKind.Text, Target = CustomFieldTarget.Bill, SortOrder = 0 },
            new CustomFieldDefinition { OwnerId = contract.Id, Name = "Losses", Kind = CustomFieldKind.Number, Target = CustomFieldTarget.Bill, SortOrder = 1 },
            new CustomFieldDefinition { OwnerId = contract.Id, Name = "Advance", Kind = CustomFieldKind.Money, Target = CustomFieldTarget.Bill, SortOrder = 2 },
            new CustomFieldDefinition { OwnerId = contract.Id, Name = "Read on", Kind = CustomFieldKind.Date, Target = CustomFieldTarget.Bill, SortOrder = 3 },
            new CustomFieldDefinition { OwnerId = contract.Id, Name = "Code", Kind = CustomFieldKind.Number, Target = CustomFieldTarget.Bill, SortOrder = 4 },
        ]);
        var vm = await EditorAsync(
            contract,
            Text + "Ref R-77\nLoss 0.35\nAdvance 12.50\nRead 26.11.2026\nCode abc\nGhost 1\n",
            P(PdfBillField.CustomField, @"^Ref (\S+)", name: "reference"),
            P(PdfBillField.CustomField, @"^Loss ([\d.]+)", name: "Losses"),
            P(PdfBillField.CustomField, @"^Advance ([\d.]+)", name: "Advance"),
            P(PdfBillField.CustomField, @"^Read (\S+)", name: "Read on"),
            P(PdfBillField.CustomField, @"^Code (\S+)", name: "Code"),
            P(PdfBillField.CustomField, @"^Ghost (\S+)", name: "No such field"));

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal("R-77", vm.FieldRows.Single(r => r.Name == "Reference").Text);
        Assert.Equal(0.35m, Number(vm.FieldRows.Single(r => r.Name == "Losses").Text));
        Assert.Equal(12.50m, Number(vm.FieldRows.Single(r => r.Name == "Advance").Text));
        var read = vm.FieldRows.Single(r => r.Name == "Read on");
        Assert.True(read.HasDate);
        Assert.Equal(new DateOnly(2026, 11, 26), read.Date);
        Assert.Equal(string.Empty, vm.FieldRows.Single(r => r.Name == "Code").Text);
        var message = LastAlert();
        Assert.Contains("PdfWarningFieldMissing No such field [en]", message);
        Assert.Contains("PdfWarningUnparsable Code|abc [en]", message);
    }

    private const string LinesPattern =
        @"^(?<description>[A-Za-z ]+?)\s+(?<quantity>[\d.]+)\s+(?<unit>m3|kWh)\s+(?<price>[\d.]+)\s+(?<net>[\d.]+)\s+(?<vat>[\d.]+)$";

    private const string LinesText = "Cold water   5.000 m3   1.410   7.05   1.48\nSewage   5.000 m3   1.200   6.00   1.26\n";

    [Fact]
    public async Task Lines_AreAdded_WhenTheBillHasNone_ButNeverOverwriteExistingLines()
    {
        var vm = await EditorAsync(
            _kit.AddWaterContract(tariff: false),
            Text + LinesText,
            P(PdfBillField.Lines, LinesPattern, combine: PdfCombine.All));

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.LineRows.Count);
        Assert.Contains("Cold water", vm.LineRows[0].Description);
        Assert.DoesNotContain("PdfWarningLinesExist", LastAlert());

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.LineRows.Count);
        Assert.Contains("PdfWarningLinesExist [en]", LastAlert());
    }

    [Fact]
    public async Task Lines_GetNoCategory_AndTheirAmounts_AndAreSavedWithTheBill()
    {
        // 7.05 + 1.48 = 8.53 for the first line
        var contract = _kit.AddWaterContract(tariff: false);
        var vm = await EditorAsync(contract, Text + LinesText, P(PdfBillField.Lines, LinesPattern, combine: PdfCombine.All));
        await vm.FillFromPdfCommand.ExecuteAsync(null);
        vm.Amounts.GrossText = "14.79";

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Bills.Saved);
        Assert.Equal(2, saved.Lines.Count);
        var first = saved.Lines[0];
        Assert.Equal("Cold water", first.Description);
        Assert.Equal(5.000m, first.Quantity);
        Assert.Equal("m3", first.Unit);
        Assert.Equal(1.410m, first.UnitPrice);
        Assert.Equal(7.05m, first.Net);
        Assert.Equal(1.48m, first.Vat);
        Assert.Equal(8.53m, first.Gross);
        Assert.Null(first.CategoryId);
    }
}
