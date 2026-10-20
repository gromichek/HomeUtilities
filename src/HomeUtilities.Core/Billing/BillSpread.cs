using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Billing;

/// <summary>Spreads a bill over the months of its service period.</summary>
public static class BillSpread
{
    /// <summary>
    /// Splits the gross amount by the number of service days in each month. Every share is rounded and the
    /// rounding remainder goes to the last month, so the shares add up to the gross exactly. Without a valid
    /// service period the whole amount belongs to <see cref="Bill.Period"/>.
    /// </summary>
    public static IReadOnlyList<(YearMonth Month, decimal Gross)> ByMonth(Bill bill)
    {
        ArgumentNullException.ThrowIfNull(bill);

        if (bill.ServiceFrom is not { } from || bill.ServiceTo is not { } to || to < from)
        {
            return [(bill.Period, bill.Gross)];
        }

        var first = YearMonth.From(from);
        var months = first.MonthsUntil(YearMonth.From(to)) + 1;
        var totalDays = to.DayNumber - from.DayNumber + 1;
        var result = new List<(YearMonth, decimal)>(months);
        var allocated = 0m;
        for (var i = 0; i < months; i++)
        {
            var month = first.AddMonths(i);
            decimal share;
            if (i == months - 1)
            {
                share = bill.Gross - allocated;
            }
            else
            {
                var start = i == 0 ? from : month.FirstDay;
                var days = month.LastDay.DayNumber - start.DayNumber + 1;
                share = MoneyMath.Round(bill.Gross * days / totalDays);
            }

            allocated += share;
            result.Add((month, share));
        }

        return result;
    }
}
