using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.Core.Tests.Tariffs;

public class TariffComponentTests
{
    private static TariffComponent Heating() => new()
    {
        Rates =
        [
            new() { ValidFrom = new(2025, 7, 1), Price = 3.50m },
            new() { ValidFrom = new(2025, 11, 1), Price = 4.56m },
        ],
    };

    [Fact]
    public void Rate_at_picks_rate_valid_on_date()
    {
        var c = Heating();

        Assert.Equal(3.50m, c.RateAt(new(2025, 10, 31))!.Price);
        Assert.Equal(4.56m, c.RateAt(new(2025, 11, 1))!.Price);
    }

    [Fact]
    public void Rate_at_before_first_rate_is_null() => Assert.Null(Heating().RateAt(new(2025, 6, 30)));

    [Fact]
    public void Rate_at_ignores_deleted_rates()
    {
        var c = Heating();
        c.Rates[1].IsDeleted = true;

        Assert.Equal(3.50m, c.RateAt(new(2025, 12, 1))!.Price);
    }
}
