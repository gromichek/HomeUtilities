using HomeUtilities.ViewModels;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.Core.Models.AndroidOption;

namespace HomeUtilities.App.Services;

/// <summary>
/// Local notifications on Android through Plugin.LocalNotification. The reminders are scheduled with the system alarm
/// manager as inexact alarms ahead of time; the app runs no service, no periodic work and no wake lock.
/// </summary>
public sealed class AndroidNotificationService : HomeUtilities.ViewModels.INotificationService
{
    /// <summary>Id of the notification channel "Reminders".</summary>
    public const string ChannelId = "reminders";

    public bool IsSupported => true;

    public bool CanSchedule => true;

    public async Task<bool> RequestPermissionAsync()
    {
        try
        {
            var center = LocalNotificationCenter.Current;
            return await center.AreNotificationsEnabled() || await center.RequestNotificationPermission();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Requesting the notification permission failed: {ex}");
            return false;
        }
    }

    public async Task ScheduleAsync(IReadOnlyList<ScheduledNotification> notifications)
    {
        foreach (var notification in notifications)
        {
            await LocalNotificationCenter.Current.Show(Request(notification, notification.At));
        }
    }

    public Task CancelAllAsync()
    {
        LocalNotificationCenter.Current.CancelAll();
        return Task.CompletedTask;
    }

    public async Task ShowNowAsync(string title, string body, string data) =>
        await LocalNotificationCenter.Current.Show(Request(new ScheduledNotification(int.MaxValue, DateTime.Now, title, body, data), null));

    // Always the inexact, idle-tolerant alarm (AlarmManager.setAndAllowWhileIdle): it needs no SCHEDULE_EXACT_ALARM, which
    // the manifest removes on purpose. The plugin's default mode would pick an exact alarm if that permission were granted.
    private static NotificationRequest Request(ScheduledNotification notification, DateTime? at) => new()
    {
        NotificationId = notification.Id,
        Title = notification.Title,
        Description = notification.Body,
        ReturningData = notification.Data,
        Android = new AndroidOptions { ChannelId = ChannelId },
        Schedule = new NotificationRequestSchedule
        {
            // The planned time is local wall-clock time; an unspecified kind is easy to read as UTC.
            NotifyTime = at is { } local ? DateTime.SpecifyKind(local, DateTimeKind.Local) : null,
            Android = new AndroidScheduleOptions
            {
                AlarmType = AndroidAlarmType.RtcWakeup,
                ScheduleMode = AndroidScheduleMode.InexactAllowWhileIdle,
            },
        },
    };
}
