using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Billing;

/// <summary>Values a new bill of a contract starts with.</summary>
public static class BillDefaults
{
    /// <summary>
    /// The due date from the contract's usual due day: in the month after the period for a bill in arrears, in the
    /// month before it for a prepaid one (it is issued and paid before the service month). A day beyond the month's
    /// length becomes the last day. Null when the contract has no due day.
    /// </summary>
    public static DateOnly? DueDate(Contract contract, YearMonth period)
    {
        ArgumentNullException.ThrowIfNull(contract);

        if (contract.PaymentDueDay is not { } day)
        {
            return null;
        }

        var month = period.AddMonths(contract.BillingTiming == BillingTiming.InAdvance ? -1 : 1);
        return new DateOnly(month.Year, month.Month, Math.Clamp(day, 1, DateTime.DaysInMonth(month.Year, month.Month)));
    }
}
