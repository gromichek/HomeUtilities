using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity;
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

        // Android 16 with targetSdk 36 uses predictive back: the legacy onBackPressed / key event is no longer delivered, so
        // the system Back minimized the app on the phone while the in-app arrow worked (owner, 2026-10-31). The AndroidX
        // dispatcher is delivered on every Android version; the callback runs the same path as the toolbar arrow.
        OnBackPressedDispatcher.AddCallback(this, new ShellBackCallback(this));

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

    /// <summary>Routes the system Back to the MAUI window (modal dialog → page → Shell); when nothing handles it, the default (leave the app).</summary>
    private sealed class ShellBackCallback(MainActivity activity) : OnBackPressedCallback(true)
    {
        public override void HandleOnBackPressed()
        {
            var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
            if (window is IWindow mauiWindow && mauiWindow.BackButtonClicked())
            {
                return;
            }

            // Not handled (Overview): let the system do its default — move the app to the background.
            Enabled = false;
            try
            {
                activity.OnBackPressedDispatcher.OnBackPressed();
            }
            finally
            {
                Enabled = true;
            }
        }
    }
}
