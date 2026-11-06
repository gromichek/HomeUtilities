using System.Globalization;

namespace HomeUtilities.ViewModels;

/// <summary>Number formats shared by the bill screens.</summary>
public static class BillText
{
    /// <summary>The space between an amount and its currency symbol: non-breaking, so an amount never wraps (UI_GUIDELINES §9).</summary>
    public const char NonBreakingSpace = '\u00A0';

    /// <summary>"46,02 €": two decimals, current culture, a non-breaking space and the currency symbol.</summary>
    public static string Money(decimal amount, string symbol) => $"{amount.ToString("N2", CultureInfo.CurrentCulture)}{NonBreakingSpace}{symbol}";

    /// <summary>"+2,15 €" / "−0,50 €": always with a sign (zero without).</summary>
    public static string SignedMoney(decimal amount, string symbol) =>
        $"{amount.ToString("+0.00;-0.00;0.00", CultureInfo.CurrentCulture)}{NonBreakingSpace}{symbol}";

    /// <summary>"+3,4 %" with a sign and one decimal.</summary>
    public static string SignedPercent(decimal percent) =>
        $"{percent.ToString("+0.0;-0.0;0.0", CultureInfo.CurrentCulture)}{NonBreakingSpace}%";

    /// <summary>"+2,15 € (+3,4 %)", or only the money part when there is no percentage.</summary>
    public static string Difference(decimal difference, decimal? percent, string symbol) =>
        percent is { } p ? $"{SignedMoney(difference, symbol)} ({SignedPercent(p)})" : SignedMoney(difference, symbol);

    /// <summary>"10.2026".</summary>
    public static string Period(Core.Common.YearMonth period) => period.FirstDay.ToString("MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>"03.11.2026".</summary>
    public static string Date(DateOnly date) => Common.DateFormat.Full(date);
}
