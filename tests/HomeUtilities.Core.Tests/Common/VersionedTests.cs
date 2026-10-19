using HomeUtilities.Core.Common;
using HomeUtilities.Core.Tests.Fakes;

namespace HomeUtilities.Core.Tests.Common;

public class VersionedTests
{
    private sealed class Rate : SyncEntity, IVersioned
    {
        public Rate(DateOnly validFrom, decimal percent)
        {
            ValidFrom = validFrom;
            Percent = percent;
        }

        public DateOnly ValidFrom { get; }

        public decimal Percent { get; }
    }

    private sealed class PlainRate(DateOnly validFrom, decimal percent) : IVersioned
    {
        public DateOnly ValidFrom { get; } = validFrom;

        public decimal Percent { get; } = percent;
    }

    private static readonly Rate[] Rates =
    [
        new(new DateOnly(2009, 1, 1), 21),
        new(new DateOnly(2011, 1, 1), 22),
        new(new DateOnly(2012, 7, 1), 21),
    ];

    [Theory]
    [InlineData(2010, 6, 15, 21)]
    [InlineData(2011, 1, 1, 22)]
    [InlineData(2012, 6, 30, 22)]
    [InlineData(2012, 7, 1, 21)]
    [InlineData(2026, 10, 19, 21)]
    public void At_ReturnsGreatestValidFromNotAfterDate(int y, int m, int d, int expected)
    {
        Assert.Equal(expected, Versioned.At(Rates, new DateOnly(y, m, d))!.Percent);
    }

    [Fact]
    public void At_BeforeFirst_ReturnsNull()
    {
        Assert.Null(Versioned.At(Rates, new DateOnly(2008, 12, 31)));
        Assert.False(Versioned.TryAt(Rates, new DateOnly(2008, 12, 31), out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryAt_Found()
    {
        Assert.True(Versioned.TryAt(Rates, new DateOnly(2011, 1, 1), out var value));
        Assert.Equal(22, value!.Percent);
    }

    [Fact]
    public void At_UnorderedInput_GivesSameResults()
    {
        var shuffled = new[] { Rates[2], Rates[0], Rates[1] };

        Assert.Equal(21, Versioned.At(shuffled, new DateOnly(2010, 6, 15))!.Percent);
        Assert.Equal(22, Versioned.At(shuffled, new DateOnly(2012, 6, 30))!.Percent);
        Assert.Equal(21, Versioned.At(shuffled, new DateOnly(2026, 10, 19))!.Percent);
        Assert.Null(Versioned.At(shuffled, new DateOnly(2008, 12, 31)));
    }

    [Fact]
    public void At_IgnoresDeletedItems()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
        var deleted = new Rate(new DateOnly(2011, 1, 1), 22);
        deleted.MarkDeleted(clock);

        var result = Versioned.At([Rates[0], deleted], new DateOnly(2012, 1, 1));

        Assert.Equal(21, result!.Percent);
    }

    [Fact]
    public void Validate_DistinctValidFrom_IsValid()
    {
        Assert.True(Versioned.Validate(Rates).IsValid);
        Assert.True(Versioned.Validate(Array.Empty<Rate>()).IsValid);
    }

    [Fact]
    public void Validate_DuplicateValidFrom_ReportsError()
    {
        var items = new[] { new PlainRate(new DateOnly(2011, 1, 1), 22), new PlainRate(new DateOnly(2011, 1, 1), 21) };

        var result = Versioned.Validate(items);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorCodes.DuplicateValidFrom, error.Code);
    }

    [Fact]
    public void Validate_DeletedDuplicate_IsIgnored()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
        var deleted = new Rate(new DateOnly(2009, 1, 1), 99);
        deleted.MarkDeleted(clock);

        Assert.True(Versioned.Validate([Rates[0], deleted]).IsValid);
    }
}
