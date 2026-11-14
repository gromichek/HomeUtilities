using HomeUtilities.Core.PdfBills;

namespace HomeUtilities.Data.Sample;

public sealed partial class SampleDataGenerator
{
    /// <summary>
    /// One invented PDF bill layout (an invented water supplier) so that the Imports screen has a layout to show and the
    /// bill editor can be tried with a PDF that carries the same texts.
    /// </summary>
    private Task AddPdfBillLayoutAsync(CancellationToken ct) =>
        SaveAsync(
            new PdfBillLayout
            {
                Name = "Example Water (sample)",
                Markers = ["SIA \"Piemērs Ūdens\"", "RĒĶINS NR."],
                Fields =
                [
                    new() { Field = PdfBillField.Number, Pattern = @"RĒĶINS NR\.:?\s+(\S+)" },
                    new() { Field = PdfBillField.IssuedOn, Pattern = @"DATUMS:?\s+(\d{2}\.\d{2}\.\d{4})" },
                    new() { Field = PdfBillField.Period, Pattern = @"PERIODS:?\s+(\d{4}\. ?gada \p{L}+)" },
                    new() { Field = PdfBillField.DueOn, Pattern = @"APMAKSAS TERMIŅŠ:?\s+(\d{2}\.\d{2}\.\d{4})" },
                    new() { Field = PdfBillField.Gross, Pattern = @"KOPĀ APMAKSAI EUR:?\s+([\d .,]+\d)" },
                    new() { Field = PdfBillField.VatPercent, Pattern = @"PVN\s+(\d+(?:[.,]\d+)?)\s*%" },
                ],
                Notes = "Invented layout for the sample data.",
            },
            pdfLayouts,
            ct);
}
