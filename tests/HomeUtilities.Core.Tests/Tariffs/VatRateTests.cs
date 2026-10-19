using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.Core.Tests.Tariffs;

public class VatRateTests
{
    private static VatRate Get(string code) => LatvianVatDefaults.Create().Single(r => r.Code == code);

    [Theory]
    [InlineData(2008, 12, 31, 18)]
    [InlineData(2009, 1, 1, 21)]
    [InlineData(2011, 6, 1, 22)]
    [InlineData(2012, 7, 1, 21)]
    [InlineData(2026, 10, 19, 21)]
    public void Standard_rate_follows_history(int y, int m, int d, int expected) =>
        Assert.Equal(expected, Get("LV-STANDARD").PercentAt(new(y, m, d)));

    [Theory]
    [InlineData(2008, 6, 1, 5)]
    [InlineData(2011, 1, 1, 12)]
    public void Reduced_rate_follows_history(int y, int m, int d, int expected) =>
        Assert.Equal(expected, Get("LV-REDUCED").PercentAt(new(y, m, d)));

    [Fact]
    public void Percent_before_first_version_is_null() =>
        Assert.Null(Get("LV-REDUCED").PercentAt(new(2004, 4, 30)));

    [Fact]
    public void Zero_rate_is_zero() => Assert.Equal(0m, Get("ZERO").PercentAt(new(2026, 1, 1)));

    [Fact]
    public void Create_returns_new_instances_each_call()
    {
        var first = LatvianVatDefaults.Create();
        var second = LatvianVatDefaults.Create();

        Assert.Equal(3, first.Count);
        Assert.NotSame(first[0], second[0]);
        Assert.NotEqual(first[0].Id, second[0].Id);
        Assert.All(first, r => Assert.All(r.Versions, v => Assert.Equal(r.Id, v.VatRateId)));
    }
}
