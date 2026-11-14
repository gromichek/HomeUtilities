using HomeUtilities.Core.Common;
using HomeUtilities.Core.PdfBills;

namespace HomeUtilities.Core.Tests.PdfBills;

public class PdfBillLayoutFileTests
{
    private const string Example = """
        { "version": 1, "layouts": [ { "name": "Example Water", "markers": ["SIA \"Piemērs Ūdens\"", "RĒĶINS NR."],
          "fields": [ { "field": "Number", "pattern": "RĒĶINS NR\\.:\\s+(\\S+)" },
                      { "field": "DueOn",  "pattern": "APMAKSAS TERMIŅŠ\\s+(\\d{2}\\.\\d{2}\\.\\d{4})" },
                      { "field": "Gross",  "pattern": "KOPĀ APMAKSAI EUR:\\s+([\\d.,]+)" } ] } ] }
        """;

    [Fact]
    public void TheExampleFile_IsRead()
    {
        var result = PdfBillLayoutFile.Read(Example);

        Assert.True(result.Succeeded);
        var layout = Assert.Single(result.Layouts);
        Assert.Equal("Example Water", layout.Name);
        Assert.Equal(["SIA \"Piemērs Ūdens\"", "RĒĶINS NR."], layout.Markers);
        Assert.Equal([PdfBillField.Number, PdfBillField.DueOn, PdfBillField.Gross], layout.Fields.Select(f => f.Field));
    }

    [Fact]
    public void WriteThenRead_KeepsSeveralLayouts()
    {
        var one = new PdfBillLayout { Name = "One", Markers = ["a"], Fields = [new() { Field = PdfBillField.Gross, Pattern = @"(\d+)" }], Notes = "note" };
        var two = new PdfBillLayout { Name = "Two", Markers = ["b", "c"], Fields = [new() { Field = PdfBillField.Period, Pattern = "x" }] };

        var json = PdfBillLayoutFile.Write([one, two]);
        var back = PdfBillLayoutFile.Read(json);

        Assert.True(back.Succeeded);
        Assert.Equal(["One", "Two"], back.Layouts.Select(l => l.Name));
        Assert.Equal("note", back.Layouts[0].Notes);
        Assert.Equal(["b", "c"], back.Layouts[1].Markers);
        Assert.Equal(PdfBillField.Period, back.Layouts[1].Fields[0].Field);
        Assert.Equal(@"(\d+)", back.Layouts[0].Fields[0].Pattern);
        Assert.DoesNotContain("createdAt", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{ \"version\": 9, \"layouts\": [] }")]
    [InlineData("{ \"version\": 1, \"layouts\": [ { \"fields\": [ { \"field\": \"Nope\" } ] } ] }")]
    [InlineData("{ \"version\": 1, \"layouts\": [ null ] }")]
    public void ABrokenFile_IsAnError_AndNeverThrows(string json)
    {
        var result = PdfBillLayoutFile.Read(json);

        Assert.False(result.Succeeded);
        Assert.Empty(result.Layouts);
        Assert.Equal(ErrorCodes.PdfLayoutFileInvalid, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void AnInvalidRegex_IsReportedWithTheFieldAndTheRegexError()
    {
        var json = """{ "version": 1, "layouts": [ { "name": "Bad", "markers": ["x"], "fields": [ { "field": "Gross", "pattern": "(unclosed" } ] } ] }""";

        var result = PdfBillLayoutFile.Read(json);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorCodes.PdfLayoutPatternInvalid, error.Code);
        Assert.Equal("Gross", error.Field);
        Assert.Equal("Bad", error.Args![0]);
        Assert.Equal("Gross", error.Args[1]);
        Assert.False(string.IsNullOrWhiteSpace(error.Args[2]?.ToString()));
        Assert.Empty(result.Layouts);
    }

    [Fact]
    public void ALayoutNeedsANameAndAMarker()
    {
        var json = """{ "version": 1, "layouts": [ { "markers": ["x"] }, { "name": "NoMarkers" } ] }""";

        var codes = PdfBillLayoutFile.Read(json).Errors.Select(e => e.Code).ToList();

        Assert.Contains(ErrorCodes.PdfLayoutNameRequired, codes);
        Assert.Contains(ErrorCodes.PdfLayoutMarkersRequired, codes);
    }

    [Fact]
    public void Merge_ReplacesALayoutWithTheSameName_InPlace_AndAddsTheOthers()
    {
        var stored = new PdfBillLayout { Name = "Example Water", Markers = ["old"], Fields = [new() { Field = PdfBillField.Gross, Pattern = "old" }] };
        var id = stored.Id;
        var other = new PdfBillLayout { Name = "Heating", Markers = ["h"] };
        var incoming = new[]
        {
            new PdfBillLayout { Name = "  example water ", Markers = ["new"], Fields = [new() { Field = PdfBillField.Number, Pattern = "new" }], Notes = "n" },
            new PdfBillLayout { Name = "Power", Markers = ["p"] },
        };

        var (updated, added) = PdfBillLayoutFile.Merge([stored, other], incoming);

        var changed = Assert.Single(updated);
        Assert.Same(stored, changed);
        Assert.Equal(id, changed.Id);
        Assert.Equal(["new"], changed.Markers);
        Assert.Equal("new", Assert.Single(changed.Fields).Pattern);
        Assert.Equal("n", changed.Notes);
        Assert.Equal("Power", Assert.Single(added).Name);
        Assert.Equal(["h"], other.Markers);
    }

    [Fact]
    public void Merge_IgnoresDeletedStoredLayouts()
    {
        var deleted = new PdfBillLayout { Name = "Water", IsDeleted = true };

        var (updated, added) = PdfBillLayoutFile.Merge([deleted], [new PdfBillLayout { Name = "Water", Markers = ["w"] }]);

        Assert.Empty(updated);
        Assert.Single(added);
    }
}
