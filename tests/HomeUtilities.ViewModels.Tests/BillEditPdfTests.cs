using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.PdfBills;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>"Fill from PDF" in the bill editor (T-055), with invented suppliers and texts. FakeLocalizer renders "key args [en]".</summary>
public class BillEditPdfTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 27);

    private const string WaterText =
        "SIA \"Piemērs Ūdens\"   RĒĶINS NR.: UD-2026-0457\n" +
        "Datums: 06.12.2026\n" +
        "Periods: 2026. gada novembris\n" +
        "APMAKSAS TERMIŅŠ 20.12.2026\n" +
        "PVN 21 %   10.33\n" +
        "KOPĀ APMAKSAI EUR: 59.49\n";

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly BillTestKit _kit = new(Today);

    public BillEditPdfTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private static PdfBillLayout WaterLayout(params PdfBillField[] without) => new()
    {
        Name = "Example Water",
        Markers = ["SIA \"Piemērs Ūdens\""],
        Fields =
        [
            .. new List<PdfBillFieldPattern>
            {
                new() { Field = PdfBillField.Number, Pattern = @"RĒĶINS NR\.:\s+(\S+)" },
                new() { Field = PdfBillField.IssuedOn, Pattern = @"Datums:\s+(\d{2}\.\d{2}\.\d{4})" },
                new() { Field = PdfBillField.Period, Pattern = @"Periods:\s+(\d{4}\. gada \p{L}+)" },
                new() { Field = PdfBillField.DueOn, Pattern = @"APMAKSAS TERMIŅŠ\s+(\d{2}\.\d{2}\.\d{4})" },
                new() { Field = PdfBillField.VatPercent, Pattern = @"PVN (\d+) %" },
                new() { Field = PdfBillField.Vat, Pattern = @"PVN \d+ %\s+([\d.]+)" },
                new() { Field = PdfBillField.Gross, Pattern = @"KOPĀ APMAKSAI EUR:\s+([\d.]+)" },
            }.Where(f => !without.Contains(f.Field)),
        ],
    };

    private async Task<BillEditViewModel> EditorAsync(Contract? contract = null, string pdfText = WaterText, params PdfBillField[] without)
    {
        contract ??= _kit.AddWaterContract(tariff: false);
        _kit.PdfLayouts.Stored.Add(WaterLayout(without));
        _kit.PdfReader.Text = pdfText;
        _kit.Kit.Media.Next = FakeMediaService.Photo("water-nov.pdf", "application/pdf", 1, 2, 3);
        var vm = _kit.CreateEditor();
        await vm.LoadAsync(null);
        _kit.Kit.Dialogs.ChooseScript = options => options.Contains(contract.Title) ? contract.Title : null;
        await vm.ChooseContractCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task TheButtonIsOffered_WhenPdfReadingIsAvailable()
    {
        var vm = await EditorAsync();

        Assert.True(vm.CanFillFromPdf);
        Assert.Equal("BillFillFromPdf [en]", vm.Texts.FillFromPdfText);
    }

    [Fact]
    public async Task Fill_SetsEveryFoundField_AsIfTyped()
    {
        var vm = await EditorAsync();

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal("UD-2026-0457", vm.Number);
        Assert.True(vm.HasIssuedOn);
        Assert.Equal(new DateOnly(2026, 12, 6), vm.IssuedOn);
        Assert.Equal(new YearMonth(2026, 11), vm.Period);
        Assert.True(vm.HasDueOn);
        Assert.Equal(new DateOnly(2026, 12, 20), vm.DueOn);
        // 59.49 − 10.33 = 49.16; the printed 21 % matches within a cent
        Assert.Equal("59.49", vm.Amounts.GrossText);
        Assert.Equal("49.16", vm.Amounts.NetText);
        Assert.Equal("10.33", vm.Amounts.VatText);
        Assert.Equal("21", vm.Amounts.VatPercentText);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task Fill_SavesNothing_AndAddsThePdfOnceAsAPendingDocument()
    {
        var vm = await EditorAsync();

        await vm.FillFromPdfCommand.ExecuteAsync(null);
        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Bills.Saved);
        var row = Assert.Single(vm.AttachmentRows);
        Assert.Contains("water-nov.pdf", row.Name);
        Assert.True(row.IsPending);
        Assert.Empty(_kit.Kit.Attachments.Added);
    }

    [Fact]
    public async Task Fill_ShowsASummary_WithLayoutFilledAndNotFoundFields()
    {
        var vm = await EditorAsync(pdfText: WaterText, without: PdfBillField.DueOn);

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        var (_, message) = Assert.Single(_kit.Kit.Dialogs.Alerts);
        Assert.Contains("PdfSummaryLayout Example Water", message);
        Assert.Contains("PdfSummaryFilled [en]", message);
        Assert.Contains("PdfField_Number [en]: UD-2026-0457", message);
        Assert.Contains("PdfField_IssuedOn [en]: 06.12.2026", message);
        Assert.Contains("PdfField_Gross [en]: 59.49", message);
        Assert.Contains("PdfSummaryNotFound PdfField_DueOn [en] [en]", message);
        Assert.Contains("PdfSummaryCheck [en]", message);
        Assert.DoesNotContain("PdfWarningOtherSupplier", message);
    }

    [Fact]
    public async Task AFieldThatIsNotFound_StaysAsItWas()
    {
        var vm = await EditorAsync(pdfText: WaterText, without: [PdfBillField.DueOn, PdfBillField.Period, PdfBillField.IssuedOn]);
        var dueBefore = vm.DueOn;

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal(dueBefore, vm.DueOn);
        Assert.Equal("UD-2026-0457", vm.Number);
    }

    [Fact]
    public async Task WithoutAPeriodInThePdf_TheContractsDefaultForTheIssueDateIsUsed()
    {
        // In arrears: a bill issued on 6 December is for November.
        var vm = await EditorAsync(without: PdfBillField.Period);

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal(new YearMonth(2026, 11), vm.Period);

        // The summary says where the period came from and does not list it as not found.
        var (_, message) = Assert.Single(_kit.Kit.Dialogs.Alerts);
        Assert.Contains("PdfSummaryPeriodFromIssue PdfField_Period [en]", message);
        Assert.DoesNotContain("PdfSummaryNotFound", message);
    }

    [Fact]
    public async Task WithoutAPeriodAndAnIssueDate_ThePeriodIsUnchanged()
    {
        var vm = await EditorAsync(without: [PdfBillField.Period, PdfBillField.IssuedOn]);
        var before = vm.Period;

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal(before, vm.Period);
    }

    [Fact]
    public async Task TotalAndTheVatRateOnly_DeriveNetAndVat()
    {
        // 34.20 / 1.21 = 28.26; VAT 5.94
        var text = "SIA \"Piemērs Ūdens\"\nPVN 21 %\nKOPĀ APMAKSAI EUR: 34.20\n";
        var vm = await EditorAsync(pdfText: text);

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal("34.20", vm.Amounts.GrossText);
        Assert.Equal("28.26", vm.Amounts.NetText);
        Assert.Equal("5.94", vm.Amounts.VatText);
        Assert.Equal("21", vm.Amounts.VatPercentText);
    }

    [Fact]
    public async Task NoFittingLayout_ChangesNothing_AndSaysWhereLayoutsAreManaged()
    {
        var vm = await EditorAsync(pdfText: "Some other supplier\nTotal 12.00");
        var number = vm.Number;
        var period = vm.Period;
        var dirty = vm.IsDirty;

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        var (_, message) = Assert.Single(_kit.Kit.Dialogs.Alerts);
        Assert.Equal("PdfNoLayout [en]", message);
        Assert.Equal(number, vm.Number);
        Assert.Equal(period, vm.Period);
        Assert.Empty(vm.AttachmentRows);
        Assert.Equal(dirty, vm.IsDirty);
    }

    [Fact]
    public async Task AnUnreadablePdf_ShowsTheReason_AndChangesNothing()
    {
        var vm = await EditorAsync();
        _kit.PdfReader.Error = new ValidationError(ErrorCodes.PdfEncrypted);

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        var (_, message) = Assert.Single(_kit.Kit.Dialogs.Alerts);
        Assert.StartsWith("PdfCannotRead", message);
        Assert.Contains("Error_pdf_encrypted", message);
        Assert.Equal(string.Empty, vm.Number);
        Assert.Empty(vm.AttachmentRows);
    }

    [Fact]
    public async Task ACancelledPicker_DoesNothing()
    {
        var vm = await EditorAsync();
        _kit.Kit.Media.Next = null;

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Equal(0, _kit.PdfReader.Calls);
        Assert.Empty(_kit.Kit.Dialogs.Alerts);
    }

    [Fact]
    public async Task ARegistrationNumberThatIsNotInThePdf_WarnsAboutAnotherSupplier_ButStillFills()
    {
        _kit.Supplier.RegistrationNumber = "LV40003123456";
        var vm = await EditorAsync();

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        var (_, message) = Assert.Single(_kit.Kit.Dialogs.Alerts);
        Assert.Contains("PdfWarningOtherSupplier [en]", message);
        Assert.Equal("UD-2026-0457", vm.Number);
    }

    [Fact]
    public async Task ARegistrationNumberThatIsInThePdf_GivesNoWarning_WithOrWithoutTheLvPrefix()
    {
        _kit.Supplier.RegistrationNumber = "LV 40003123456";
        var vm = await EditorAsync(pdfText: WaterText + "Reģ. Nr. 40003123456\n");

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.DoesNotContain("PdfWarningOtherSupplier", Assert.Single(_kit.Kit.Dialogs.Alerts).Message);
    }

    [Fact]
    public async Task ASupplierWithoutARegistrationNumber_IsNotChecked()
    {
        _kit.Supplier.RegistrationNumber = null;
        var vm = await EditorAsync();

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.DoesNotContain("PdfWarningOtherSupplier", Assert.Single(_kit.Kit.Dialogs.Alerts).Message);
    }

    [Fact]
    public async Task AValueThatDoesNotParse_IsReportedAsAWarning_AndLeftAlone()
    {
        var text = WaterText.Replace("06.12.2026", "no date");
        var layout = WaterLayout();
        layout.Fields.First(f => f.Field == PdfBillField.IssuedOn).Pattern = @"Datums:\s+(.+)";
        var contract = _kit.AddWaterContract(tariff: false);
        _kit.PdfLayouts.Stored.Add(layout);
        _kit.PdfReader.Text = text;
        _kit.Kit.Media.Next = FakeMediaService.Photo("w.pdf", "application/pdf");
        var vm = _kit.CreateEditor();
        await vm.LoadAsync(null);

        await vm.FillFromPdfCommand.ExecuteAsync(null);

        Assert.Contains("PdfWarningUnparsable PdfField_IssuedOn [en]|no date", Assert.Single(_kit.Kit.Dialogs.Alerts).Message);
        Assert.Equal(Today, vm.IssuedOn);
        _ = contract;
    }

    [Fact]
    public async Task TheButtonIsHidden_WithoutAPdfReader()
    {
        var kit = _kit.Kit;
        var vm = new BillEditViewModel(
            _kit.Bills, _kit.Payments, kit.Contracts, kit.Suppliers, kit.Categories, kit.Properties, kit.ChargeLoader, kit.Plans, kit.Attachments,
            kit.Media, kit.Viewer, _kit.QuickPay, _kit.Templates, _kit.TemplateLauncher, kit.Prepayments, kit.Clock, kit.Localizer, kit.Dialogs, kit.Navigation, kit.Toast, kit.ErrorLog);
        await vm.LoadAsync(null);

        Assert.False(vm.CanFillFromPdf);
    }
}
