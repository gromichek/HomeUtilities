using HomeUtilities.Core.Overview;
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

    /// <summary>
    /// Nothing paid and the due date is within the next few days (amber). Only the bills list uses it — it needs today's date,
    /// which <see cref="BillBadge.KindOf"/> does not have.
    /// </summary>
    DueSoon,
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

    /// <summary>
    /// <see cref="KindOf(BillState)"/>, plus <see cref="BillBadgeKind.DueSoon"/> for an unpaid bill that falls due within the next
    /// days (UI_GUIDELINES §8). One rule for the Bills list and the bill editor (C-12).
    /// </summary>
    public static BillBadgeKind KindOf(BillState state, DateOnly? dueOn, DateOnly? today)
    {
        var kind = KindOf(state);
        return kind == BillBadgeKind.Unpaid && today is { } now && dueOn is { } due && due >= now && due <= now.AddDays(TodoBuilder.BillLookAheadDays)
            ? BillBadgeKind.DueSoon
            : kind;
    }

    /// <summary>"Unpaid", "Partially paid · open 26,02 €", "Overdue", "Paid ✓", "Prepaid ✓".</summary>
    public static string TextOf(ILocalizer localizer, BillState state, string currencySymbol) =>
        TextOf(localizer, state, currencySymbol, KindOf(state), null);

    /// <summary>The text of <paramref name="kind"/>; "Due 25.11.2026" for <see cref="BillBadgeKind.DueSoon"/>.</summary>
    public static string TextOf(ILocalizer localizer, BillState state, string currencySymbol, BillBadgeKind kind, DateOnly? dueOn)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(state);

        var open = BillText.Money(state.Open, currencySymbol);
        return kind switch
        {
            BillBadgeKind.DueSoon when dueOn is { } due => localizer.Get("BillBadgeDueSoon", BillText.Date(due)),
            BillBadgeKind.Prepaid => localizer["BillBadgePrepaid"],
            BillBadgeKind.Paid => localizer["BillBadgePaid"],
            BillBadgeKind.Overdue => state.Paid != 0 ? localizer.Get("BillBadgeOverdueOpen", open) : localizer["BillBadgeOverdue"],
            BillBadgeKind.PartiallyPaid => localizer.Get("BillBadgePartial", open),
            _ => localizer["BillBadgeUnpaid"],
        };
    }
}
