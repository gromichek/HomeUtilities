namespace HomeUtilities.Core.Reminders;

/// <summary>
/// The owner's reminder choices. They are stored per device (not in the synced database), because every device
/// schedules its own notifications.
/// </summary>
/// <param name="Enabled">Master switch; off = nothing is planned or scheduled.</param>
/// <param name="Readings">Remind about reading windows.</param>
/// <param name="Bills">Remind about bills and tax installments that are due.</param>
/// <param name="Verification">Remind about meter verification.</param>
/// <param name="Drafts">Remind about draft bills to confirm.</param>
/// <param name="TimeOfDay">The time of day the reminders are shown.</param>
/// <param name="BillLeadDays">Days before the due date a bill reminder is shown (a template may override it).</param>
public sealed record ReminderSettings(
    bool Enabled = false,
    bool Readings = true,
    bool Bills = true,
    bool Verification = true,
    bool Drafts = true,
    TimeOnly TimeOfDay = default,
    int BillLeadDays = ReminderSettings.DefaultBillLeadDays)
{
    /// <summary>Default time of day.</summary>
    public static readonly TimeOnly DefaultTime = new(9, 0);

    /// <summary>Default lead of bill reminders.</summary>
    public const int DefaultBillLeadDays = 2;

    /// <summary>Largest lead of bill reminders.</summary>
    public const int MaxBillLeadDays = 14;

    /// <summary>The time of day; 09:00 when none was set.</summary>
    public TimeOnly Time => TimeOfDay == default ? DefaultTime : TimeOfDay;

    /// <summary>The lead clamped to 0–14 days.</summary>
    public int LeadDays => Math.Clamp(BillLeadDays, 0, MaxBillLeadDays);

    /// <summary>True if reminders of <paramref name="kind"/> are wanted.</summary>
    public bool Wants(ReminderKind kind) => Enabled && kind switch
    {
        ReminderKind.SubmitReadings => Readings,
        ReminderKind.PayBill or ReminderKind.TaxInstallment => Bills,
        ReminderKind.MeterVerification => Verification,
        ReminderKind.DraftsToConfirm => Drafts,
        _ => false,
    };
}
