using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Attachments;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.PdfBills;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

// "Fill from PDF" (T-055): reads the header of the supplier's PDF with a layout and puts the values into the form as if typed.
public sealed partial class BillEditViewModel
{
    /// <summary>True where PDF reading is available (the button is shown).</summary>
    public bool CanFillFromPdf => _pdfReader is not null && _pdfLayouts is not null;

    /// <summary>
    /// Picks a PDF, finds the layout that fits it and fills the found fields. Nothing is saved; with no fitting layout or an
    /// unreadable file nothing changes in the form.
    /// </summary>
    [RelayCommand]
    private Task FillFromPdfAsync() => RunSafeAsync(async () =>
    {
        if (_pdfReader is null || _pdfLayouts is null)
        {
            return;
        }

        AttachmentError = string.Empty;
        var (file, error) = await PhotoPicking.TryPickAsync(() => _media.PickFileAsync([AttachmentContentTypes.Pdf]), _localizer);
        if (error.Length > 0)
        {
            AttachmentError = error;
            return;
        }

        if (file is null)
        {
            return;
        }

        PdfTextResult read;
        await using (var content = await file.OpenAsync())
        {
            read = await _pdfReader.ReadAsync(content);
        }

        var title = _localizer["BillFillFromPdf"];
        if (read.Error is { } readError)
        {
            await Dialogs.AlertAsync(title, _localizer.Get("PdfCannotRead", ValidationText.For(_localizer, readError)));
            return;
        }

        var result = PdfBillReader.Read(read.Text, await _pdfLayouts.ListAsync());
        if (!result.LayoutFound)
        {
            await Dialogs.AlertAsync(title, _localizer["PdfNoLayout"]);
            return;
        }

        var supplierWarning = await IsOtherSupplierAsync(read.Text);
        ApplyPdfValues(result.Values);
        AddPendingPdf(file);
        await Dialogs.AlertAsync(title, BuildPdfSummary(result, supplierWarning));
    });

    private void ApplyPdfValues(PdfBillValues values)
    {
        // Every assignment goes through the same property the user edits, so checks, defaults and the comparison follow.
        var issuedOn = values.IssuedOn;
        var period = values.Period
            ?? (issuedOn is { } issued && !IsOneOff && _contract is not null ? Bill.DefaultPeriod(_contract, issued) : (YearMonth?)null);
        if (period is { } month)
        {
            Period = month;
        }

        if (values.Number is { } number)
        {
            Number = number.Length > InputLimits.Name ? number[..InputLimits.Name] : number;
        }

        if (issuedOn is { } issuedDate)
        {
            HasIssuedOn = true;
            IssuedOn = issuedDate;
        }

        if (values.DueOn is { } dueDate)
        {
            HasDueOn = true;
            DueOn = dueDate;
        }

        ApplyPdfAmounts(values);
    }

    private void ApplyPdfAmounts(PdfBillValues values)
    {
        var amounts = PdfBillAmounts.Resolve(values);
        if (amounts is null)
        {
            return;
        }

        if (values.Vat is not null)
        {
            Amounts.Fill(amounts);
        }
        else if (amounts.VatPercent is { } percent && values.VatPercent is not null)
        {
            Amounts.GrossText = AmountEditor.FormatMoney(amounts.Gross);
            Amounts.VatPercentText = DecimalInput.Format(percent, CultureInfo.CurrentCulture, 2);
        }
        else
        {
            Amounts.GrossText = AmountEditor.FormatMoney(amounts.Gross);
        }
    }

    private void AddPendingPdf(PickedFile file)
    {
        var name = Path.GetFileName(file.FileName);
        var known = _storedAttachments.Select(a => a.FileName).Concat(_pendingFiles.Select(f => Path.GetFileName(f.FileName)));
        if (known.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _pendingFiles.Add(file);
        RebuildAttachmentRows();
        MarkDirty();
    }

    // True when the contract's supplier has a registration number and the PDF does not contain it.
    private async Task<bool> IsOtherSupplierAsync(string pdfText)
    {
        var contract = _contract;
        if (IsOneOff || contract is null)
        {
            return false;
        }

        var supplier = await _supplierStore.GetAsync(contract.SupplierId);
        var digits = new string([.. (supplier?.RegistrationNumber ?? string.Empty).Where(char.IsDigit)]);
        return digits.Length > 0 && !pdfText.Contains(digits, StringComparison.Ordinal);
    }

    private string BuildPdfSummary(PdfBillResult result, bool otherSupplier)
    {
        var lines = new List<string> { _localizer.Get("PdfSummaryLayout", result.LayoutName ?? string.Empty), string.Empty };
        var found = result.Found.Select(f => $"{FieldName(f)}: {FormatPdfValue(result.Values, f)}").ToList();
        if (found.Count > 0)
        {
            lines.Add(_localizer["PdfSummaryFilled"]);
            lines.AddRange(found);
        }

        if (result.NotFound.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add(_localizer.Get("PdfSummaryNotFound", string.Join(", ", result.NotFound.Select(FieldName))));
        }

        foreach (var warning in result.Warnings)
        {
            lines.Add(string.Empty);
            lines.Add(warning.Kind == PdfBillWarningKind.Timeout
                ? _localizer.Get("PdfWarningTimeout", FieldName(warning.Field))
                : _localizer.Get("PdfWarningUnparsable", FieldName(warning.Field), warning.Raw ?? string.Empty));
        }

        if (otherSupplier)
        {
            lines.Add(string.Empty);
            lines.Add(_localizer["PdfWarningOtherSupplier"]);
        }

        lines.Add(string.Empty);
        lines.Add(_localizer["PdfSummaryCheck"]);
        return string.Join(Environment.NewLine, lines);
    }

    private string FieldName(PdfBillField field) => _localizer["PdfField_" + field];

    private static string FormatPdfValue(PdfBillValues values, PdfBillField field) => field switch
    {
        PdfBillField.Number => values.Number ?? string.Empty,
        PdfBillField.IssuedOn => values.IssuedOn is { } issued ? DateFormat.Full(issued) : string.Empty,
        PdfBillField.DueOn => values.DueOn is { } due ? DateFormat.Full(due) : string.Empty,
        PdfBillField.Period => values.Period is { } period ? MonthFormat.Long(period) : string.Empty,
        PdfBillField.Gross => values.Gross is { } gross ? AmountEditor.FormatMoney(gross) : string.Empty,
        PdfBillField.Vat => values.Vat is { } vat ? AmountEditor.FormatMoney(vat) : string.Empty,
        PdfBillField.VatPercent => values.VatPercent is { } percent ? DecimalInput.Format(percent, CultureInfo.CurrentCulture, 2) + " %" : string.Empty,
        _ => string.Empty,
    };
}
