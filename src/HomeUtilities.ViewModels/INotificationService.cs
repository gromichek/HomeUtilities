namespace HomeUtilities.ViewModels;

/// <summary>One system notification: shown at <see cref="At"/> even when the app is closed (where the platform allows it).</summary>
/// <param name="Id">Stable number derived from the reminder key (see <see cref="ReminderScheduler.IdOf"/>).</param>
/// <param name="At">Local time to show it.</param>
/// <param name="Title">Title text in the current language.</param>
/// <param name="Body">Body text in the current language.</param>
/// <param name="Data">The serialized <c>ReminderTarget</c> a tap opens.</param>
public sealed record ScheduledNotification(int Id, DateTime At, string Title, string Body, string Data);

/// <summary>
/// System notifications. Android hands pre-scheduled notifications to the system (nothing of ours runs in the background);
/// Windows shows notifications only while the app runs or not at all. Implemented by the platform layer.
/// </summary>
public interface INotificationService
{
    /// <summary>False if the device cannot show notifications at all; the settings then say that reminders appear when the app is opened.</summary>
    bool IsSupported { get; }

    /// <summary>True if notifications can be scheduled ahead for a time the app is closed.</summary>
    bool CanSchedule { get; }

    /// <summary>Asks the system for the permission to show notifications (Android 13+); true if it is granted.</summary>
    Task<bool> RequestPermissionAsync();

    /// <summary>Schedules the notifications. Callers cancel the old ones first (<see cref="CancelAllAsync"/>).</summary>
    Task ScheduleAsync(IReadOnlyList<ScheduledNotification> notifications);

    /// <summary>
    /// Ids of the notifications the system currently holds for later ("Planned reminders" marks the planned ones that are
    /// missing). Empty where nothing can be scheduled.
    /// </summary>
    Task<IReadOnlyCollection<int>> GetPendingIdsAsync() => Task.FromResult<IReadOnlyCollection<int>>([]);

    /// <summary>Removes every pending notification of the app.</summary>
    Task CancelAllAsync();

    /// <summary>Shows a notification right now (the test notification, the summary on start). Does nothing when unsupported.</summary>
    Task ShowNowAsync(string title, string body, string data);
}
