using HomeUtilities.Core.Billing;

namespace HomeUtilities.ViewModels;

/// <summary>The payment status shown as a badge (docs/PAYMENT_TYPES.md, "Payment status of a bill").</summary>
public enum BillBadgeKind
{
    /// <summary>Nothing paid (grey).</summary>
    Unpaid,

    /// <summary>Part of the total is paid (yellow, shows the open amount).</summary>
    PartiallyPaid,

    /// <summary>Open and past the due date (red).</summary>
    Overdue,

    /// <summary>Everything paid (green ✓).</summary>
    Paid,

    /// <summary>Paid before the service month started (green ✓).</summary>
    Prepaid,

    /// <summary>Created from a template and not confirmed yet (muted); counts nowhere until confirmed.</summary>
    Draft,
}

/// <summary>Maps a <see cref="BillState"/> to the badge and its text.</summary>
public static class BillBadge
{
    /// <summary>Paid or prepaid when nothing is open; else overdue when past due; else partially paid when something was paid.</summary>
    public static BillBadgeKind KindOf(BillState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.IsPaid)
        {
            return state.IsPrepaid ? BillBadgeKind.Prepaid : BillBadgeKind.Paid;
        }

        if (state.IsOverdue)
        {
            return BillBadgeKind.Overdue;
        }

        return state.Paid != 0 ? BillBadgeKind.PartiallyPaid : BillBadgeKind.Unpaid;
    }

    /// <summary>"Unpaid", "Partially paid · open 26,02 €", "Overdue", "Paid ✓", "Prepaid ✓".</summary>
    public static string TextOf(ILocalizer localizer, BillState state, string currencySymbol)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(state);

        var open = BillText.Money(state.Open, currencySymbol);
        return KindOf(state) switch
        {
            BillBadgeKind.Prepaid => localizer["BillBadgePrepaid"],
            BillBadgeKind.Paid => localizer["BillBadgePaid"],
            BillBadgeKind.Overdue => state.Paid != 0 ? localizer.Get("BillBadgeOverdueOpen", open) : localizer["BillBadgeOverdue"],
            BillBadgeKind.PartiallyPaid => localizer.Get("BillBadgePartial", open),
            _ => localizer["BillBadgeUnpaid"],
        };
    }
}
