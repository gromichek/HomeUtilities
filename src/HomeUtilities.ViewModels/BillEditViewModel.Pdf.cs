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
        var report = await ApplyPdfValuesAsync(result.Values);
        AddPendingPdf(file);
        await Dialogs.AlertAsync(title, BuildPdfSummary(result, supplierWarning, report));
    });

    /// <summary>What applying the PDF values did, beyond what the reader found: skipped fields, extra lines and warnings.</summary>
    private sealed class PdfApplyReport
    {
        /// <summary>The period taken from the issue date when the PDF has none (shown in the summary).</summary>
        public YearMonth? DerivedPeriod { get; set; }

        /// <summary>Found fields that were not applied (they are not listed as filled).</summary>
        public HashSet<PdfBillField> Skipped { get; } = [];

        /// <summary>Filled values of named entries (tariff lines, custom fields), in the order applied.</summary>
        public List<string> Filled { get; } = [];

        /// <summary>Warnings of the editor (unknown tariff line, bill already has lines…).</summary>
        public List<string> Warnings { get; } = [];
    }

    // Header first (the period decides the quantity rows), then everything that depends on rows of the form.
    private async Task<PdfApplyReport> ApplyPdfValuesAsync(PdfBillValues values)
    {
        var report = new PdfApplyReport { DerivedPeriod = ApplyPdfValues(values) };
        ApplyPdfAmounts(values, report);

        // A changed period rebuilds the quantity rows asynchronously: wait for it, do not race.
        await PendingRefresh;

        ApplyPdfServicePeriod(values, report);
        if (values.Title is { } title)
        {
            BillTitle = Clip(title, InputLimits.Text);
        }

        if (values.Consumption is { } consumption)
        {
            ConsumptionText = DecimalInput.Format(consumption, CultureInfo.CurrentCulture, 4);
        }

        ApplyPdfQuantities(values, report);
        ApplyPdfCustomFields(values, report);
        ApplyPdfNotes(values, report);
        ApplyPdfLines(values, report);
        return report;
    }

    private static string Clip(string text, int max) => text.Length > max ? text[..max] : text;

    private YearMonth? ApplyPdfValues(PdfBillValues values)
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

        return values.Period is null ? period : null;
    }

    private void ApplyPdfAmounts(PdfBillValues values, PdfApplyReport report)
    {
        var amounts = PdfBillAmounts.Resolve(values);
        if (amounts is null)
        {
            if (values.Net is not null)
            {
                report.Skipped.Add(PdfBillField.Net);
                report.Warnings.Add(_localizer["PdfWarningNetNeedsTotal"]);
            }

            return;
        }

        if (values.Vat is not null || values.Net is not null)
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

    private void ApplyPdfServicePeriod(PdfBillValues values, PdfApplyReport report)
    {
        if (values.ServiceFrom is { } from && values.ServiceTo is { } to)
        {
            HasServicePeriod = true;
            ServiceFrom = from;
            ServiceTo = to;
        }
        else if (values.ServiceFrom is not null || values.ServiceTo is not null)
        {
            report.Skipped.Add(PdfBillField.ServiceFrom);
            report.Skipped.Add(PdfBillField.ServiceTo);
            report.Warnings.Add(_localizer["PdfWarningServicePeriodPartial"]);
        }
    }

    // Quantities and amounts of tariff lines go to the row of the line with that name (case-insensitive) — same path as typing.
    private void ApplyPdfQuantities(PdfBillValues values, PdfApplyReport report)
    {
        var missing = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var quantity in values.Quantities)
        {
            if (FindQuantityRow(quantity.Name) is not { } row)
            {
                NoteMissingComponent(quantity.Name, missing, report);
                continue;
            }

            row.Text = DecimalInput.Format(quantity.Value, CultureInfo.CurrentCulture, 4);
            report.Filled.Add($"{row.Name}: {row.Text}");
        }

        foreach (var amount in values.ComponentAmounts)
        {
            if (FindQuantityRow(amount.Name) is not { } row)
            {
                NoteMissingComponent(amount.Name, missing, report);
                continue;
            }

            row.AmountText = DecimalInput.Format(amount.Value, CultureInfo.CurrentCulture, 2);
            report.Filled.Add($"{AmountEntryName(row.Name)}: {row.AmountText}");
        }
    }

    private BillQuantityRowViewModel? FindQuantityRow(string name) =>
        QuantityRows.FirstOrDefault(r => string.Equals(r.Name.Trim(), name.Trim(), StringComparison.CurrentCultureIgnoreCase));

    private void NoteMissingComponent(string name, HashSet<string> missing, PdfApplyReport report)
    {
        if (missing.Add(name))
        {
            report.Warnings.Add(_localizer.Get("PdfWarningComponentMissing", name));
        }
    }

    private string AmountEntryName(string component) => $"{component} ({_localizer["BillQuantityAmount"]})";

    // A custom field is found by its name; its value is read by the field's kind (the layout does not know the kind).
    private void ApplyPdfCustomFields(PdfBillValues values, PdfApplyReport report)
    {
        foreach (var custom in values.CustomFields)
        {
            var row = FieldRows.FirstOrDefault(r => string.Equals(r.Name.Trim(), custom.Name.Trim(), StringComparison.CurrentCultureIgnoreCase));
            if (row is null)
            {
                report.Warnings.Add(_localizer.Get("PdfWarningFieldMissing", custom.Name));
                continue;
            }

            string? shown = null;
            switch (row.Kind)
            {
                case CustomFieldKind.Number or CustomFieldKind.Money:
                    if (custom.Sum is { } sum || PdfValueParsers.TryParseDecimal(custom.Raw, out sum))
                    {
                        row.Text = DecimalInput.Format(sum, CultureInfo.CurrentCulture, 4);
                        shown = row.Text;
                    }

                    break;
                case CustomFieldKind.Date:
                    if (custom.Sum is null && PdfValueParsers.TryParseDate(custom.Raw, out var date))
                    {
                        row.HasDate = true;
                        row.Date = date;
                        shown = DateFormat.Full(date);
                    }

                    break;
                default:
                    if (custom.Sum is null)
                    {
                        row.Text = Clip(custom.Raw.Trim(), InputLimits.Text);
                        shown = row.Text;
                    }

                    break;
            }

            if (shown is null)
            {
                report.Warnings.Add(_localizer.Get("PdfWarningUnparsable", custom.Name, custom.Raw));
            }
            else
            {
                report.Filled.Add($"{row.Name}: {shown}");
            }
        }
    }

    // The text goes on a new line after the existing comment, and is not added twice.
    private void ApplyPdfNotes(PdfBillValues values, PdfApplyReport report)
    {
        if (values.Notes is not { Length: > 0 } text)
        {
            return;
        }

        var existing = Notes ?? string.Empty;
        if (existing.Contains(text, StringComparison.Ordinal))
        {
            return;
        }

        var combined = existing.Trim().Length == 0 ? text : existing.TrimEnd() + Environment.NewLine + text;
        if (combined.Length > InputLimits.Notes)
        {
            combined = combined[..InputLimits.Notes];
            report.Warnings.Add(_localizer.Get("PdfWarningNotesTooLong", InputLimits.Notes));
        }

        Notes = combined;
    }

    // Lines only for a bill that has none yet; they get no category of their own, like a line typed by hand.
    private void ApplyPdfLines(PdfBillValues values, PdfApplyReport report)
    {
        if (values.Lines.Count == 0)
        {
            return;
        }

        if (_lines.Count > 0)
        {
            report.Skipped.Add(PdfBillField.Lines);
            report.Warnings.Add(_localizer["PdfWarningLinesExist"]);
            return;
        }

        foreach (var found in values.Lines)
        {
            var net = found.Net is { } n ? MoneyMath.Round(n) : (decimal?)null;
            var vat = found.Vat is { } v ? MoneyMath.Round(v) : (decimal?)null;
            var gross = net is { } netAmount
                ? netAmount + (vat ?? 0m)
                : found.Quantity is { } q && found.UnitPrice is { } price ? MoneyMath.Round(q * price) : 0m;
            _lines.Add(new BillLine
            {
                BillId = _bill.Id,
                Description = Clip(found.Description, InputLimits.Text),
                Quantity = found.Quantity,
                Unit = found.Unit is { } unit ? Clip(unit, InputLimits.Name) : null,
                UnitPrice = found.UnitPrice,
                Net = net,
                Vat = vat,
                VatPercent = net is { } base1 && vat is { } vat1 && base1 != 0m ? Math.Round(vat1 / base1 * 100m, 2) : null,
                Gross = gross,
                SortOrder = _lines.Count,
            });
        }

        LinesExpanded = true;
        RebuildLines();
        MarkDirty();
    }

    private string BuildPdfSummary(PdfBillResult result, bool otherSupplier, PdfApplyReport report)
    {
        var lines = new List<string> { _localizer.Get("PdfSummaryLayout", result.LayoutName ?? string.Empty), string.Empty };
        var found = result.Found
            .Where(f => !report.Skipped.Contains(f))
            .Select(f => $"{FieldName(f)}: {FormatPdfValue(result.Values, f)}")
            .ToList();
        if (report.DerivedPeriod is { } derived)
        {
            found.Add(_localizer.Get("PdfSummaryPeriodFromIssue", FieldName(PdfBillField.Period), MonthFormat.Long(derived)));
        }

        found.AddRange(report.Filled);

        var notFound = result.NotFound
            .Where(f => report.DerivedPeriod is null || f != PdfBillField.Period)
            .Select(FieldName)
            .Concat(result.NotFoundNamed.Select(NamedEntryName))
            .ToList();
        if (found.Count > 0)
        {
            lines.Add(_localizer["PdfSummaryFilled"]);
            lines.AddRange(found);
        }

        if (notFound.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add(_localizer.Get("PdfSummaryNotFound", string.Join(", ", notFound)));
        }

        foreach (var warning in result.Warnings)
        {
            lines.Add(string.Empty);
            lines.Add(warning.Kind switch
            {
                PdfBillWarningKind.Timeout => _localizer.Get("PdfWarningTimeout", WarningName(warning)),
                PdfBillWarningKind.AmountsMismatch => _localizer["PdfWarningAmountsMismatch"],
                _ => _localizer.Get("PdfWarningUnparsable", WarningName(warning), warning.Raw ?? string.Empty),
            });
        }

        foreach (var warning in report.Warnings)
        {
            lines.Add(string.Empty);
            lines.Add(warning);
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

    // A tariff line or a custom field is named by its own name, not by the field kind.
    private string NamedEntryName(PdfBillNamedEntry entry) =>
        entry.Field == PdfBillField.ComponentAmount ? AmountEntryName(entry.Subject) : entry.Subject;

    private string WarningName(PdfBillWarning warning) =>
        warning.Subject is null
            ? FieldName(warning.Field)
            : NamedEntryName(new PdfBillNamedEntry(warning.Field, warning.Subject));

    private string FormatPdfValue(PdfBillValues values, PdfBillField field) => field switch
    {
        PdfBillField.Number => values.Number ?? string.Empty,
        PdfBillField.IssuedOn => values.IssuedOn is { } issued ? DateFormat.Full(issued) : string.Empty,
        PdfBillField.DueOn => values.DueOn is { } due ? DateFormat.Full(due) : string.Empty,
        PdfBillField.Period => values.Period is { } period ? MonthFormat.Long(period) : string.Empty,
        PdfBillField.Gross => values.Gross is { } gross ? AmountEditor.FormatMoney(gross) : string.Empty,
        PdfBillField.Vat => values.Vat is { } vat ? AmountEditor.FormatMoney(vat) : string.Empty,
        PdfBillField.Net => values.Net is { } net ? AmountEditor.FormatMoney(net) : string.Empty,
        PdfBillField.VatPercent => values.VatPercent is { } percent ? DecimalInput.Format(percent, CultureInfo.CurrentCulture, 2) + " %" : string.Empty,
        PdfBillField.ServiceFrom => values.ServiceFrom is { } from ? DateFormat.Full(from) : string.Empty,
        PdfBillField.ServiceTo => values.ServiceTo is { } to ? DateFormat.Full(to) : string.Empty,
        PdfBillField.Title => values.Title ?? string.Empty,
        PdfBillField.Consumption => values.Consumption is { } consumption ? DecimalInput.Format(consumption, CultureInfo.CurrentCulture, 4) : string.Empty,
        PdfBillField.Notes => Shorten(values.Notes ?? string.Empty, 80),
        PdfBillField.Lines => _localizer.Get("PdfSummaryLinesAdded", values.Lines.Count),
        _ => string.Empty,
    };

    private static string Shorten(string text, int max) => text.Length > max ? text[..(max - 1)] + "…" : text;
}
