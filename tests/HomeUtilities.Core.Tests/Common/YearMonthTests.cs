using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Tests.Common;

public class YearMonthTests
{
    [Fact]
    public void From_FirstDay_LastDay()
    {
        var ym = YearMonth.From(new DateOnly(2026, 10, 19));

        Assert.Equal(new YearMonth(2026, 10), ym);
        Assert.Equal(new DateOnly(2026, 10, 1), ym.FirstDay);
        Assert.Equal(new DateOnly(2026, 10, 31), ym.LastDay);
    }

    [Theory]
    [InlineData(2024, 2, 29)]
    [InlineData(2026, 2, 28)]
    public void LastDay_February(int year, int month, int day) =>
        Assert.Equal(new DateOnly(year, month, day), new YearMonth(year, month).LastDay);

    [Fact]
    public void AddMonths_AcrossYearBoundary()
    {
        Assert.Equal(new YearMonth(2027, 2), new YearMonth(2026, 11).AddMonths(3));
        Assert.Equal(new YearMonth(2025, 12), new YearMonth(2026, 1).AddMonths(-1));
        Assert.Equal(new YearMonth(2026, 5), new YearMonth(2026, 5).AddMonths(0));
    }

    [Fact]
    public void MonthsUntil_CanBeNegative()
    {
        Assert.Equal(15, new YearMonth(2026, 11).MonthsUntil(new YearMonth(2028, 2)));
        Assert.Equal(-3, new YearMonth(2027, 2).MonthsUntil(new YearMonth(2026, 11)));
        Assert.Equal(0, new YearMonth(2026, 1).MonthsUntil(new YearMonth(2026, 1)));
    }

    [Fact]
    public void Comparisons()
    {
        var a = new YearMonth(2025, 12);
        var b = new YearMonth(2026, 1);

        Assert.True(a < b);
        Assert.True(a <= b);
        Assert.True(b > a);
        Assert.True(b >= a);
        var a2 = new YearMonth(2025, 12);
        Assert.True(a <= a2);
        Assert.True(a >= a2);
        Assert.False(b < a);
        Assert.Equal(-1, Math.Sign(a.CompareTo(b)));
    }

    [Fact]
    public void Contains_ChecksYearAndMonth()
    {
        var ym = new YearMonth(2026, 10);

        Assert.True(ym.Contains(new DateOnly(2026, 10, 31)));
        Assert.False(ym.Contains(new DateOnly(2026, 11, 1)));
        Assert.False(ym.Contains(new DateOnly(2025, 10, 15)));
    }

    [Fact]
    public void ToString_Parse_RoundTrip()
    {
        var ym = new YearMonth(2026, 3);

        Assert.Equal("2026-03", ym.ToString());
        Assert.Equal(ym, YearMonth.Parse("2026-03"));
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-1")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("2026/10")]
    [InlineData("1800-01")]
    public void TryParse_RejectsInvalid(string? text)
    {
        Assert.False(YearMonth.TryParse(text, out var value));
        Assert.Equal(default, value);
        Assert.Throws<FormatException>(() => YearMonth.Parse(text!));
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(1899, 1)]
    [InlineData(2201, 1)]
    public void Constructor_RejectsOutOfRange(int year, int month) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new YearMonth(year, month));
}
