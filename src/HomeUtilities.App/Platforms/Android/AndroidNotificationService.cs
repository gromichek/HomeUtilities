using Android.Content;
using Android.Provider;
using HomeUtilities.ViewModels;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.Core.Models.AndroidOption;

namespace HomeUtilities.App.Services;

/// <summary>
/// Local notifications on Android. The reminders are scheduled by our own code (<see cref="ReminderAlarms"/>: inexact
/// alarms of the system alarm manager, nothing runs in the background, no exact-alarm permission, no wake lock). The
/// Plugin.LocalNotification package is used only for the permission request, the channel, the tap event and notifications
/// shown at once; it schedules nothing, so exactly one component owns the alarms.
/// </summary>
public sealed class AndroidNotificationService : HomeUtilities.ViewModels.INotificationService
{
    /// <summary>Id of the notification channel "Reminders".</summary>
    public const string ChannelId = ReminderAlarms.ChannelId;

    private static Context AppContext => Android.App.Application.Context;

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

    public Task<IReadOnlyList<ScheduleFailure>> ScheduleAsync(IReadOnlyList<ScheduledNotification> notifications) =>
        Task.Run<IReadOnlyList<ScheduleFailure>>(() =>
        {
            var context = AppContext;
            RemoveLegacyPluginAlarms(context);

            var failures = new List<ScheduleFailure>();
            var registered = new List<StoredReminder>();
            foreach (var notification in notifications)
            {
                try
                {
                    var reminder = new StoredReminder(
                        notification.Id, ReminderAlarms.ToUtcMillis(notification.At), notification.Title, notification.Body, notification.Data);
                    ReminderAlarms.Register(context, reminder);
                    registered.Add(reminder);
                }
                catch (Exception ex)
                {
                    failures.Add(new ScheduleFailure(notification.Id, notification.At, ex.Message));
                }
            }

            // The new alarms are in place; only now the stored plan is replaced and the alarms that are no longer planned go.
            var old = ReminderAlarms.LoadPlan(context);
            ReminderAlarms.SavePlan(context, registered);
            var keep = registered.Select(r => r.Id).ToHashSet();
            foreach (var stale in old.Where(r => !keep.Contains(r.Id)))
            {
                ReminderAlarms.Cancel(context, stale.Id);
            }

            return failures;
        });

    public Task<IReadOnlyCollection<int>> GetHeldIdsAsync(IReadOnlyCollection<int> ids) =>
        Task.FromResult<IReadOnlyCollection<int>>([.. ids.Where(id => ReminderAlarms.IsHeld(AppContext, id))]);

    public Task<bool> AreNotificationsEnabledAsync() => Task.FromResult(ReminderAlarms.AreNotificationsEnabled(AppContext));

    public Task OpenNotificationSettingsAsync()
    {
        var context = AppContext;
        var intent = new Intent(Settings.ActionAppNotificationSettings);
        intent.PutExtra(Settings.ExtraAppPackage, context.PackageName);
        intent.AddFlags(ActivityFlags.NewTask);
        context.StartActivity(intent);
        return Task.CompletedTask;
    }

    public Task CancelAllAsync() =>
        Task.Run(() =>
        {
            var context = AppContext;
            RemoveLegacyPluginAlarms(context);
            var old = ReminderAlarms.LoadPlan(context);
            ReminderAlarms.SavePlan(context, []);
            foreach (var reminder in old)
            {
                ReminderAlarms.Cancel(context, reminder.Id);
            }
        });

    public Task<bool> ScheduleTestAsync(DateTime at, string title, string body, string data) =>
        Task.Run(() =>
        {
            // Not stored in the plan: a reschedule neither replaces nor cancels it; the alarm forgets it once shown.
            ReminderAlarms.Register(AppContext, new StoredReminder(ReminderAlarms.TestId, ReminderAlarms.ToUtcMillis(at), title, body, data));
            return true;
        });

    public async Task ShowNowAsync(string title, string body, string data) =>
        await LocalNotificationCenter.Current.Show(new NotificationRequest
        {
            NotificationId = int.MaxValue,
            Title = title,
            Description = body,
            ReturningData = data,
            Android = new AndroidOptions
            {
                ChannelId = ChannelId,
                VisibilityType = AndroidVisibilityType.Public,
                Priority = AndroidPriority.High,
                IconSmallName = new AndroidIcon("ic_stat_reminder"),
            },
        });

    // Versions before T-028e scheduled through the plugin, which keeps its own list of alarms and re-registers them after a
    // restart. Once per device they are removed, so that no second component can deliver a reminder twice.
    private static void RemoveLegacyPluginAlarms(Context context)
    {
        if (ReminderAlarms.LegacyCleared(context))
        {
            return;
        }

        try
        {
            LocalNotificationCenter.Current.CancelAll();
            ReminderAlarms.MarkLegacyCleared(context);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Removing the plugin alarms failed: {ex}");
        }
    }
}
