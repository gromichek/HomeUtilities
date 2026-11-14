using HomeUtilities.Core.PdfBills;

namespace HomeUtilities.Core.Tests.PdfBills;

public class PdfLineBuilderTests
{
    // A word of 5 units per character, so a normal space is about 2 units and a column gap far more.
    private static PdfWord Word(string text, double x, double y) => new(text, x, y, text.Length * 5);

    [Fact]
    public void TwoColumnsOnOneBaseline_AreOneLineWithAtLeastTwoSpaces()
    {
        var text = PdfLineBuilder.BuildPage([Word("Total", 50, 700), Word("59.49", 300, 700)]);

        var line = Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.StartsWith("Total  ", line);
        Assert.EndsWith("  59.49", line);
        Assert.True(line.Length > "Total  59.49".Length);
    }

    [Fact]
    public void WordsWithANormalGap_AreJoinedByOneSpace()
    {
        var text = PdfLineBuilder.BuildPage([Word("Bill", 50, 700), Word("number", 77, 700)]);

        Assert.Equal("Bill number\n", text);
    }

    [Fact]
    public void BaselinesOnePointApart_AreTheSameLine()
    {
        var text = PdfLineBuilder.BuildPage([Word("Due", 50, 700), Word("on", 67, 699), Word("30.11.2026", 79, 700.8)]);

        Assert.Equal("Due on 30.11.2026\n", text);
    }

    [Fact]
    public void Lines_RunTopToBottom_AndWordsLeftToRight_WhateverTheInputOrder()
    {
        var text = PdfLineBuilder.BuildPage(
        [
            Word("second", 50, 600),
            Word("B", 58, 700),
            Word("A", 50, 700),
            Word("third", 50, 500),
        ]);

        Assert.Equal("A B\nsecond\nthird\n", text);
    }

    [Fact]
    public void Pages_AreSeparatedByAnEmptyLine()
    {
        var text = PdfLineBuilder.Build([[Word("one", 50, 700)], [Word("two", 50, 700)]]);

        Assert.Equal("one\n\ntwo\n", text);
    }

    [Fact]
    public void Diacritics_AreKept()
    {
        var text = PdfLineBuilder.BuildPage([Word("RĒĶINS", 50, 700), Word("ūdens", 82, 700)]);

        Assert.Equal("RĒĶINS ūdens\n", text);
    }

    [Fact]
    public void NoWords_GiveNoText() => Assert.Equal(string.Empty, PdfLineBuilder.Build([]));
}
