using HomeUtilities.Core.Common;
using HomeUtilities.Core.PdfBills;
using HomeUtilities.Import;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace HomeUtilities.Import.Tests;

public class PdfTextReaderTests
{
    // A synthetic bill made in the test; no real PDF is kept in the repository.
    private static byte[] BuildBill(bool withText = true)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        if (withText)
        {
            page.AddText("BILL NO.:", 12, new PdfPoint(50, 780), font);
            page.AddText("UD-2026-0457", 12, new PdfPoint(300, 780), font);
            page.AddText("TOTAL EUR:", 12, new PdfPoint(50, 760), font);
            page.AddText("59,49", 12, new PdfPoint(300, 760), font);
            page.AddText("Due on 20.11.2026", 12, new PdfPoint(50, 740), font);
        }

        var second = builder.AddPage(PageSize.A4);
        if (withText)
        {
            second.AddText("Page two", 12, new PdfPoint(50, 780), font);
        }

        return builder.Build();
    }

    [Fact]
    public async Task ASyntheticPdf_IsReadAsLines_WithColumnsSeparatedByTwoOrMoreSpaces()
    {
        using var stream = new MemoryStream(BuildBill());

        var result = await new PdfTextReader().ReadAsync(stream);

        Assert.True(result.Succeeded, result.Error?.Code);
        var lines = result.Text.Split('\n');
        Assert.Matches(@"^BILL NO\.:\s{2,}UD-2026-0457$", lines[0]);
        Assert.Matches(@"^TOTAL EUR:\s{2,}59,49$", lines[1]);
        Assert.Equal("Due on 20.11.2026", lines[2]);
        Assert.Equal(string.Empty, lines[3]);
        Assert.Equal("Page two", lines[4]);
    }

    [Fact]
    public async Task ThePdfText_FeedsTheLayoutReader()
    {
        using var stream = new MemoryStream(BuildBill());
        var text = (await new PdfTextReader().ReadAsync(stream)).Text;
        var layout = new PdfBillLayout
        {
            Name = "Synthetic",
            Markers = ["BILL NO."],
            Fields =
            [
                new() { Field = PdfBillField.Number, Pattern = @"BILL NO\.:\s+(\S+)" },
                new() { Field = PdfBillField.Gross, Pattern = @"TOTAL EUR:\s+([\d.,]+)" },
                new() { Field = PdfBillField.DueOn, Pattern = @"Due on (\d{2}\.\d{2}\.\d{4})" },
            ],
        };

        var result = PdfBillReader.Read(text, layout);

        Assert.Equal("UD-2026-0457", result.Values.Number);
        Assert.Equal(59.49m, result.Values.Gross);
        Assert.Equal(new DateOnly(2026, 11, 20), result.Values.DueOn);
    }

    [Fact]
    public async Task APdfWithoutText_GivesNoTextError()
    {
        using var stream = new MemoryStream(BuildBill(withText: false));

        var result = await new PdfTextReader().ReadAsync(stream);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.PdfNoText, result.Error!.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("this is not a pdf at all")]
    public async Task ABrokenFile_GivesAnErrorInsteadOfThrowing(string content)
    {
        using var stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(content));

        var result = await new PdfTextReader().ReadAsync(stream);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.PdfUnreadable, result.Error!.Code);
        Assert.Equal(string.Empty, result.Text);
    }

    [Fact]
    public async Task ATruncatedPdf_GivesAnError()
    {
        var bytes = BuildBill();
        using var stream = new MemoryStream(bytes, 0, bytes.Length / 3);

        var result = await new PdfTextReader().ReadAsync(stream);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AFileOverTwentyMegabytes_IsRefused()
    {
        using var stream = new MemoryStream(new byte[IPdfTextReader.MaxBytes + 1]);

        var result = await new PdfTextReader().ReadAsync(stream);

        Assert.Equal(ErrorCodes.PdfTooLarge, result.Error!.Code);
    }

    [Fact]
    public async Task ACancelledRead_Throws_NotAnError()
    {
        using var stream = new MemoryStream(BuildBill());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PdfTextReader().ReadAsync(stream, cts.Token));
    }
}
