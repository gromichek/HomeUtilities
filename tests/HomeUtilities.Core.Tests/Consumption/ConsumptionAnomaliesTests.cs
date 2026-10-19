using HomeUtilities.Core.Consumption;

namespace HomeUtilities.Core.Tests.Consumption;

public class ConsumptionAnomaliesTests
{
    private static string[] Codes(decimal current, decimal[] history) =>
        [.. ConsumptionAnomalies.Check(current, history).Select(w => w.Code)];

    [Fact]
    public void Above_three_times_average_is_unusually_high()
    {
        // average of [10, 11, 9] = 10; 3 x 10 = 30
        Assert.Equal([ConsumptionWarningCodes.UnusuallyHigh], Codes(40m, [10m, 11m, 9m]));
        Assert.Empty(Codes(25m, [10m, 11m, 9m]));
        Assert.Empty(Codes(30m, [10m, 11m, 9m]));
    }

    [Fact]
    public void Custom_factor()
    {
        Assert.Empty(ConsumptionAnomalies.Check(25m, [10m, 11m, 9m], 3m));
        Assert.Single(ConsumptionAnomalies.Check(25m, [10m, 11m, 9m], 2m));
    }

    [Fact]
    public void Short_history_gives_no_high_warning() => Assert.Empty(Codes(40m, [10m, 11m]));

    [Fact]
    public void Zero_with_positive_average()
    {
        Assert.Equal([ConsumptionWarningCodes.ZeroConsumption], Codes(0m, [10m, 11m]));
        Assert.Empty(Codes(0m, [0m, 0m, 0m]));
        Assert.Empty(Codes(0m, []));
    }
}
