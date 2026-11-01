namespace HomeUtilities.ViewModels;

/// <summary>Why the plan was handed to the system (shown in "Planned reminders" as "Last update").</summary>
public enum ReminderRescheduleReason
{
    /// <summary>The app started.</summary>
    Start,

    /// <summary>The app came back to the foreground.</summary>
    Resume,

    /// <summary>Data changed (bills, payments, readings, templates, contracts, meters, import, restore…).</summary>
    Change,

    /// <summary>The reminder settings changed or the planned list was opened.</summary>
    Settings,

    /// <summary>The language changed (the texts of the notifications are in the new language).</summary>
    Language,

    /// <summary>The app was left: a change that was still waiting for the debounce was handed over at once.</summary>
    LeavingApp,
}

/// <summary>The result of the last hand-over of the plan to the system.</summary>
/// <param name="At">Local time of the hand-over.</param>
/// <param name="Reason">Why it ran.</param>
/// <param name="Planned">How many reminders were planned.</param>
/// <param name="Failed">How many of them the system refused.</param>
public sealed record ReminderScheduleStatus(DateTime At, ReminderRescheduleReason Reason, int Planned, int Failed);

/// <summary>Sent after every hand-over of the plan to the system, so open screens can refresh their diagnostics.</summary>
public sealed record ReminderScheduleChangedMessage(ReminderScheduleStatus Status);
