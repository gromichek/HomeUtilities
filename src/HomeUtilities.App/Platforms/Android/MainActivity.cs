using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using HomeUtilities.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HomeUtilities.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // A recreated activity (savedInstanceState != null) carries the old intent: that tap was already handled.
        if (savedInstanceState is null)
        {
            OpenReminderTarget(Intent);
        }
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        OpenReminderTarget(intent);
    }

    // A tap on a reminder (shown by ReminderAlarms) carries the serialized target; the router opens it now, or as soon as
    // the window is ready if the tap started the app.
    private static void OpenReminderTarget(Intent? intent)
    {
        var target = intent?.GetStringExtra(ReminderAlarms.TargetExtra);
        if (target is null)
        {
            return;
        }

        intent!.RemoveExtra(ReminderAlarms.TargetExtra);
        IPlatformApplication.Current?.Services.GetService<NotificationTapRouter>()?.Submit(target);
    }
}
