using System.Globalization;

namespace HomeUtilities.ViewModels;

/// <summary>Text formats shared by the payment screens.</summary>
public static class PaymentText
{
    /// <summary>"58,32 €", or "−12,00 € refund" for a negative amount (a refund received).</summary>
    public static string Amount(ILocalizer localizer, decimal amount, string symbol)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        return amount < 0
            ? localizer.Get("PaymentRefundAmount", BillText.Money(-amount, symbol))
            : BillText.Money(amount, symbol);
    }

    /// <summary>The sign of an amount typed by the user: -1 for a refund, else +1.</summary>
    public static int SignOf(decimal amount) => amount < 0 ? -1 : 1;

    /// <summary>"Water 09.2026" — a bill's title and period.</summary>
    public static string BillName(string title, Core.Common.YearMonth period) =>
        string.IsNullOrWhiteSpace(title) ? BillText.Period(period) : $"{title} {BillText.Period(period)}";

    /// <summary>Money for the amount fields: two decimals, current culture.</summary>
    public static string Field(decimal amount) => amount.ToString("0.00", CultureInfo.CurrentCulture);
}
