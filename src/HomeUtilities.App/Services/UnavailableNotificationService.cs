using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

/// <summary>
/// Used where the app cannot show or schedule system notifications (Windows, unpackaged app): the settings say so and the
/// reminders appear in the app when it is opened (the start summary as an in-app message).
/// </summary>
public sealed class UnavailableNotificationService : INotificationService
{
    public bool IsSupported => false;

    public bool CanSchedule => false;

    public Task<bool> RequestPermissionAsync() => Task.FromResult(false);

    public Task<IReadOnlyList<ScheduleFailure>> ScheduleAsync(IReadOnlyList<ScheduledNotification> notifications) =>
        Task.FromResult<IReadOnlyList<ScheduleFailure>>([]);

    public Task CancelAllAsync() => Task.CompletedTask;

    public Task ShowNowAsync(string title, string body, string data) => Task.CompletedTask;
}
