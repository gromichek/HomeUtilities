using Android.App;
using Android.Content;
using AndroidX.Core.App;
using Org.Json;

namespace HomeUtilities.App.Services;

/// <summary>One reminder as the app hands it to the Android alarm manager, and as it is stored for the boot receiver.</summary>
/// <param name="Id">Notification id; also the request code of the alarm.</param>
/// <param name="AtUtcMillis">Delivery time, milliseconds since 1970 (UTC).</param>
/// <param name="Title">Notification title.</param>
/// <param name="Body">Notification text.</param>
/// <param name="Data">The serialized reminder target a tap opens.</param>
public sealed record StoredReminder(int Id, long AtUtcMillis, string Title, string Body, string Data);

/// <summary>
/// The only place that schedules reminder alarms (owner decision 2026-10-29: inexact alarms, no exact alarm permission, no
/// service, no wake lock). An alarm is <c>AlarmManager.SetAndAllowWhileIdle</c> with a broadcast to
/// <see cref="ReminderAlarmReceiver"/>; the handed-over plan is also stored in a private preference file so that the boot
/// receiver can register the alarms again. Everything here uses plain Android APIs only (it runs in receivers, possibly
/// before any of our services exist).
/// </summary>
public static class ReminderAlarms
{
    /// <summary>The notification channel created at startup (see MauiProgram).</summary>
    public const string ChannelId = "reminder_alerts";

    /// <summary>
    /// Earlier channels, deleted at start. Android never changes the importance or lock-screen visibility of an existing
    /// channel, so each fix needs a new id: "reminders" (private on the lock screen), "reminders_public" (default importance:
    /// no pop-up, not shown on the owner's lock screen, 2026-11-01). The current one has high importance and public visibility.
    /// </summary>
    public static readonly string[] OldChannelIds = ["reminders", "reminders_public"];

    /// <summary>Notification id of the delayed test notification (never produced by the reminder id hash in practice).</summary>
    public const int TestId = int.MaxValue - 1;

    /// <summary>Intent extra of the activity intent: the serialized target of a tapped reminder.</summary>
    public const string TargetExtra = "homeutilities.reminder.target";

    private const string AlarmAction = "io.github.gromichek.homeutilities.REMINDER_ALARM";
    private const string PreferencesName = "homeutilities.reminders";
    private const string PlanKey = "plan";
    private const string LegacyClearedKey = "legacy_plugin_cleared";
    private const string IdExtra = "id";
    private const string AtExtra = "at";
    private const string TitleExtra = "title";
    private const string BodyExtra = "body";
    private const string DataExtra = "data";

    /// <summary>
    /// How late a reminder may still be shown. An inexact alarm is often delivered minutes (in Doze, hours) late; the same
    /// limit applies to reminders missed while the phone was off.
    /// </summary>
    public static readonly TimeSpan AllowedDelay = TimeSpan.FromHours(6);

    /// <summary>Registers (or replaces) the alarm of a reminder.</summary>
    public static void Register(Context context, StoredReminder reminder)
    {
        var alarms = AlarmManagerOf(context);
        var pending = PendingIntent.GetBroadcast(context, reminder.Id, AlarmIntent(context, reminder), PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)
            ?? throw new InvalidOperationException("The system returned no PendingIntent.");
        alarms.SetAndAllowWhileIdle(AlarmType.RtcWakeup, reminder.AtUtcMillis, pending);
    }

    /// <summary>Cancels the alarm of a reminder (does nothing if there is none).</summary>
    public static void Cancel(Context context, int id)
    {
        var pending = Existing(context, id);
        if (pending is null)
        {
            return;
        }

        AlarmManagerOf(context).Cancel(pending);
        pending.Cancel();
    }

    /// <summary>Asks Android itself whether the alarm of this reminder exists (not a list we wrote).</summary>
    public static bool IsHeld(Context context, int id) => Existing(context, id) is not null;

    /// <summary>The handed-over plan, as stored.</summary>
    public static IReadOnlyList<StoredReminder> LoadPlan(Context context)
    {
        var json = Preferences(context).GetString(PlanKey, null);
        var result = new List<StoredReminder>();
        if (string.IsNullOrEmpty(json))
        {
            return result;
        }

        try
        {
            var array = new JSONArray(json);
            for (var i = 0; i < array.Length(); i++)
            {
                var item = array.GetJSONObject(i);
                if (item is null)
                {
                    continue;
                }

                result.Add(new StoredReminder(
                    item.GetInt(IdExtra),
                    item.GetLong(AtExtra),
                    item.OptString(TitleExtra, string.Empty) ?? string.Empty,
                    item.OptString(BodyExtra, string.Empty) ?? string.Empty,
                    item.OptString(DataExtra, string.Empty) ?? string.Empty));
            }
        }
        catch (JSONException)
        {
            // A damaged store is treated as empty; the next hand-over from the app rewrites it.
            result.Clear();
        }

        return result;
    }

    /// <summary>Stores the handed-over plan.</summary>
    public static void SavePlan(Context context, IEnumerable<StoredReminder> plan)
    {
        var array = new JSONArray();
        foreach (var reminder in plan)
        {
            var item = new JSONObject();
            item.Put(IdExtra, reminder.Id);
            item.Put(AtExtra, reminder.AtUtcMillis);
            item.Put(TitleExtra, new Java.Lang.String(reminder.Title));
            item.Put(BodyExtra, new Java.Lang.String(reminder.Body));
            item.Put(DataExtra, new Java.Lang.String(reminder.Data));
            array.Put(item);
        }

        Preferences(context).Edit()?.PutString(PlanKey, array.ToString())?.Commit();
    }

    /// <summary>True once the alarms of the old plugin-based scheduler were removed on this device (see <see cref="MarkLegacyCleared"/>).</summary>
    public static bool LegacyCleared(Context context) => Preferences(context).GetBoolean(LegacyClearedKey, false);

    /// <summary>Remembers that the old plugin-based alarms were removed.</summary>
    public static void MarkLegacyCleared(Context context) => Preferences(context).Edit()?.PutBoolean(LegacyClearedKey, true)?.Commit();

    /// <summary>The alarm went off: shows the reminder unless it is too late, and forgets it.</summary>
    public static void OnAlarm(Context context, Intent intent)
    {
        var reminder = new StoredReminder(
            intent.GetIntExtra(IdExtra, -1),
            intent.GetLongExtra(AtExtra, 0),
            intent.GetStringExtra(TitleExtra) ?? string.Empty,
            intent.GetStringExtra(BodyExtra) ?? string.Empty,
            intent.GetStringExtra(DataExtra) ?? string.Empty);
        if (reminder.Id < 0)
        {
            return;
        }

        SavePlan(context, LoadPlan(context).Where(r => r.Id != reminder.Id));
        if (IsTooLate(reminder, Now()))
        {
            return;
        }

        Show(context, reminder);
    }

    /// <summary>
    /// After a restart of the phone or an update of the app: registers the future alarms again, shows the ones missed by
    /// not more than <see cref="AllowedDelay"/> and forgets the older ones. Returns the number of failures.
    /// </summary>
    public static int RestoreAfterBoot(Context context)
    {
        var now = Now();
        var keep = new List<StoredReminder>();
        var failures = 0;
        foreach (var reminder in LoadPlan(context))
        {
            try
            {
                if (reminder.AtUtcMillis > now)
                {
                    Register(context, reminder);
                    keep.Add(reminder);
                }
                else if (!IsTooLate(reminder, now))
                {
                    Show(context, reminder);
                }
            }
            catch (Exception)
            {
                failures++;
            }
        }

        SavePlan(context, keep);
        return failures;
    }

    /// <summary>Shows a notification on the reminders channel (text visible on the lock screen); a tap opens the app on the reminder's target.</summary>
    public static void Show(Context context, StoredReminder reminder)
    {
        EnsureChannel(context);
        var open = new Intent(context, typeof(MainActivity));
        open.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        open.PutExtra(TargetExtra, reminder.Data);
        var tap = PendingIntent.GetActivity(context, reminder.Id, open, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        // The binding marks every builder method as nullable; the calls are made one by one on the same builder.
        var builder = new NotificationCompat.Builder(context, ChannelId);
        // A one-colour small icon: the coloured app icon is not valid as a status bar icon and Samsung's lock screen /
        // Always On Display may skip such notifications (owner, 2026-11-01: WhatsApp lights up the locked screen, we did not).
        _ = builder.SetSmallIcon(Resource.Drawable.ic_stat_reminder);
        // The small icon itself is one colour; Android tints its circle in the shade with this colour (the app's primary
        // blue) — that is the coloured icon seen in WhatsApp's notifications.
        _ = builder.SetColor(unchecked((int)0xFF2F5D8A));
        _ = builder.SetContentTitle(reminder.Title);
        _ = builder.SetContentText(reminder.Body);
        _ = builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(reminder.Body));
        _ = builder.SetContentIntent(tap);
        _ = builder.SetAutoCancel(true);
        _ = builder.SetPriority(NotificationCompat.PriorityHigh);
        _ = builder.SetCategory(NotificationCompat.CategoryReminder);
        _ = builder.SetVisibility(NotificationCompat.VisibilityPublic);
        var notification = builder.Build();
        NotificationManagerCompat.From(context)!.Notify(reminder.Id, notification);
    }

    /// <summary>True if the system lets the app show notifications.</summary>
    public static bool AreNotificationsEnabled(Context context) => NotificationManagerCompat.From(context)!.AreNotificationsEnabled();

    /// <summary>Current time, milliseconds since 1970 (UTC).</summary>
    public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>Delivery time of a local wall-clock time as milliseconds since 1970 (UTC).</summary>
    public static long ToUtcMillis(DateTime local) => new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Local)).ToUnixTimeMilliseconds();

    private static bool IsTooLate(StoredReminder reminder, long now) => now - reminder.AtUtcMillis > (long)AllowedDelay.TotalMilliseconds;

    private static Intent AlarmIntent(Context context, StoredReminder reminder)
    {
        var intent = AlarmIntentKey(context);
        intent.PutExtra(IdExtra, reminder.Id);
        intent.PutExtra(AtExtra, reminder.AtUtcMillis);
        intent.PutExtra(TitleExtra, reminder.Title);
        intent.PutExtra(BodyExtra, reminder.Body);
        intent.PutExtra(DataExtra, reminder.Data);
        return intent;
    }

    // Extras do not take part in the matching of PendingIntents: component and action do, and the request code is the id.
    private static Intent AlarmIntentKey(Context context)
    {
        var intent = new Intent(context, typeof(ReminderAlarmReceiver));
        intent.SetAction(AlarmAction);
        return intent;
    }

    private static PendingIntent? Existing(Context context, int id) =>
        PendingIntent.GetBroadcast(context, id, AlarmIntentKey(context), PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable);

    private static AlarmManager AlarmManagerOf(Context context) =>
        context.GetSystemService(Context.AlarmService) as AlarmManager ?? throw new InvalidOperationException("The alarm service is not available.");

    private static ISharedPreferences Preferences(Context context) =>
        context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)
            ?? throw new InvalidOperationException("The preferences are not available.");

    // Normally created at app start (MauiProgram); created here too in case a receiver runs before that.
    private static void EnsureChannel(Context context)
    {
        if (context.GetSystemService(Context.NotificationService) is not NotificationManager manager || manager.GetNotificationChannel(ChannelId) is not null)
        {
            return;
        }

        manager.CreateNotificationChannel(new NotificationChannel(ChannelId, "Reminders", NotificationImportance.High)
        {
            LockscreenVisibility = NotificationVisibility.Public,
        });
    }

    /// <summary>Deletes the earlier reminder channels; see <see cref="OldChannelIds"/>.</summary>
    public static void DeleteOldChannel(Context context)
    {
        if (context.GetSystemService(Context.NotificationService) is not NotificationManager manager)
        {
            return;
        }

        foreach (var id in OldChannelIds)
        {
            if (manager.GetNotificationChannel(id) is not null)
            {
                manager.DeleteNotificationChannel(id);
            }
        }
    }
}
