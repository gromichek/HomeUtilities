using HomeUtilities.Core.PdfBills;
using Xunit.Abstractions;

namespace HomeUtilities.Import.Tests;

/// <summary>
/// Reads every real PDF of <c>tests/LocalData/pdf-bills/</c> with the layouts of <c>tests/LocalData/pdf-layouts.json</c>
/// (see <see cref="LocalDataFactAttribute"/>). Skipped when the folder is missing. The found values go to the test output.
/// </summary>
public class PdfLocalBillsTests(ITestOutputHelper output)
{
    [LocalDataFact]
    public async Task EveryLocalPdf_FitsALayout_AndGivesNumberDueDateAndTotal()
    {
        var folder = LocalDataFactAttribute.LocalDataFolder!;
        var layoutFile = PdfBillLayoutFile.Read(await File.ReadAllTextAsync(Path.Combine(folder, "pdf-layouts.json")));
        Assert.True(layoutFile.Succeeded, string.Join("; ", layoutFile.Errors.Select(e => e.Code + " " + string.Join(" ", e.Args ?? []))));

        var reader = new PdfTextReader();
        var files = Directory.GetFiles(Path.Combine(folder, "pdf-bills"), "*.pdf");
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            await using var stream = File.OpenRead(file);
            var text = await reader.ReadAsync(stream);
            Assert.True(text.Succeeded, $"{Path.GetFileName(file)}: {text.Error?.Code}");

            var result = PdfBillReader.Read(text.Text, layoutFile.Layouts);
            Assert.True(result.LayoutFound, $"{Path.GetFileName(file)}: no layout fits");

            var v = result.Values;
            output.WriteLine($"{Path.GetFileName(file)} → {result.LayoutName}: number={v.Number}, issued={v.IssuedOn}, period={v.Period}, due={v.DueOn}, gross={v.Gross}, vat%={v.VatPercent}, vat={v.Vat}; not found: [{string.Join(", ", result.NotFound)}]");
            foreach (var warning in result.Warnings)
            {
                output.WriteLine($"  warning {warning.Field}: {warning.Kind} {warning.Raw}");
            }

            Assert.NotNull(v.Number);
            Assert.NotNull(v.DueOn);
            Assert.NotNull(v.Gross);
        }
    }
}
