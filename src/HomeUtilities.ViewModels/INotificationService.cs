namespace HomeUtilities.ViewModels;

/// <summary>One system notification: shown at <see cref="At"/> even when the app is closed (where the platform allows it).</summary>
/// <param name="Id">Stable number derived from the reminder key (see <see cref="ReminderScheduler.IdOf"/>).</param>
/// <param name="At">Local time to show it.</param>
/// <param name="Title">Title text in the current language.</param>
/// <param name="Body">Body text in the current language.</param>
/// <param name="Data">The serialized <c>ReminderTarget</c> a tap opens.</param>
public sealed record ScheduledNotification(int Id, DateTime At, string Title, string Body, string Data);

/// <summary>A reminder the system refused to take (see <see cref="INotificationService.ScheduleAsync"/>).</summary>
/// <param name="Id">Notification id.</param>
/// <param name="At">Local time it was planned for.</param>
/// <param name="Reason">Why it was refused (exception message or a short explanation).</param>
public sealed record ScheduleFailure(int Id, DateTime At, string Reason);

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

    /// <summary>
    /// Makes the system hold exactly these notifications: the new ones are registered first, only then the old ones that are
    /// not in the list are cancelled, so a failure never leaves the user without reminders. Returns the notifications the
    /// system refused (empty = all taken); it does not throw for a single refused notification.
    /// </summary>
    Task<IReadOnlyList<ScheduleFailure>> ScheduleAsync(IReadOnlyList<ScheduledNotification> notifications);

    /// <summary>
    /// Which of the given ids the system really holds for later (asked from the system itself, not from a list of ours).
    /// "Planned reminders" marks the ones that are missing. Empty where nothing can be scheduled.
    /// </summary>
    Task<IReadOnlyCollection<int>> GetHeldIdsAsync(IReadOnlyCollection<int> ids) => Task.FromResult<IReadOnlyCollection<int>>([]);

    /// <summary>False if the user switched the notifications of the app off in the system settings.</summary>
    Task<bool> AreNotificationsEnabledAsync() => Task.FromResult(true);

    /// <summary>Opens the notification settings of the app in the system settings.</summary>
    Task OpenNotificationSettingsAsync() => Task.CompletedTask;

    /// <summary>
    /// Schedules one test notification at <paramref name="at"/> (local time) through the same path as the reminders, so it
    /// can be checked with the screen locked. It is not part of the plan and is never cancelled by a reschedule.
    /// False where nothing can be scheduled.
    /// </summary>
    Task<bool> ScheduleTestAsync(DateTime at, string title, string body, string data) => Task.FromResult(false);

    /// <summary>Removes every pending notification of the app.</summary>
    Task CancelAllAsync();

    /// <summary>Shows a notification right now (the test notification, the summary on start). Does nothing when unsupported.</summary>
    Task ShowNowAsync(string title, string body, string data);
}
