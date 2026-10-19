namespace HomeUtilities.Core.Common;

/// <summary>Conversion between the supported currencies.</summary>
public static class CurrencyConverter
{
    /// <summary>Fixed conversion rate: 1 EUR = 0.702804 LVL.</summary>
    public const decimal LvlPerEur = 0.702804m;

    /// <summary>Converts an amount to EUR. The result is not rounded; callers round.</summary>
    public static decimal ToEur(decimal amount, Currency from) => from switch
    {
        Currency.EUR => amount,
        Currency.LVL => amount / LvlPerEur,
        _ => throw new ArgumentOutOfRangeException(nameof(from), from, "Unknown currency."),
    };
}
