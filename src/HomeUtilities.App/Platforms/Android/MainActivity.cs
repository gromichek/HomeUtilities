using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity;

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
