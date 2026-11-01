using Android.Content;
using HomeUtilities.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HomeUtilities.App.Services;

/// <summary>Receives the reminder alarm from the alarm manager and shows the notification (nothing runs between the alarms).</summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public class ReminderAlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
        {
            return;
        }

        try
        {
            ReminderAlarms.OnAlarm(context, intent);
        }
        catch (Exception ex)
        {
            ReminderReceiverLog.Append("Reminder alarm", ex);
        }
    }
}

/// <summary>
/// After a restart of the phone or an update of the app Android has forgotten the alarms: registers the stored plan again
/// (no database work, no network).
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[Android.App.IntentFilter(new[] { Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced, "android.intent.action.QUICKBOOT_POWERON" })]
public class ReminderBootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null)
        {
            return;
        }

        try
        {
            var failures = ReminderAlarms.RestoreAfterBoot(context);
            if (failures > 0)
            {
                ReminderReceiverLog.Append("Reminder boot", new InvalidOperationException($"{failures} reminder(s) could not be registered again after the restart."));
            }
        }
        catch (Exception ex)
        {
            ReminderReceiverLog.Append("Reminder boot", ex);
        }
    }
}

internal static class ReminderReceiverLog
{
    // The error log is a service of the app; a receiver may run before it exists, then the failure is only traced.
    public static void Append(string source, Exception exception)
    {
        try
        {
            if (IPlatformApplication.Current?.Services.GetService<IErrorLog>() is { } log)
            {
                log.Append(source, exception);
                return;
            }
        }
        catch (Exception)
        {
            // Fall through to the trace.
        }

        System.Diagnostics.Debug.WriteLine($"{source}: {exception}");
    }
}
