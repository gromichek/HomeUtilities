using Android.Graphics.Drawables;
using AndroidX.Core.View;
using Microsoft.Maui.Platform;

namespace HomeUtilities.App;

/// <summary>
/// The Android navigation bar (◁ ○ ▢ or the gesture bar) in the page colour with matching button contrast, in both themes
/// (owner 2026-11-05). Android 15+ draws the app edge-to-edge and ignores the bar colour: what shows behind the bar is the window
/// background, so that is set to the page colour, and the scrim the system adds for contrast is switched off. Up to Android 14
/// the bar colour itself is set. The status bar is handled in <c>AppShell.ApplyStatusBar</c>.
/// </summary>
internal static class SystemBars
{
    /// <summary>Applies the colours of the current theme; call at start and whenever the theme changes.</summary>
    public static void Apply()
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window is not { } window
            || app is null
            || !app.Resources.TryGetValue(app.RequestedTheme == AppTheme.Dark ? "SurfaceDark" : "SurfaceLight", out var value)
            || value is not Color page)
        {
            return;
        }

        var dark = app.RequestedTheme == AppTheme.Dark;
        var color = page.ToPlatform();

        window.SetBackgroundDrawable(new ColorDrawable(color));
        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            // Deprecated from Android 15, where the bar is transparent and the window background shows through instead.
#pragma warning disable CA1422, CS0618
            window.SetNavigationBarColor(color);
#pragma warning restore CA1422, CS0618
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            window.NavigationBarContrastEnforced = false;
        }

        // Dark buttons on a light bar, light buttons on a dark bar.
        if (WindowCompat.GetInsetsController(window, window.DecorView!) is { } controller)
        {
            controller.AppearanceLightNavigationBars = !dark;
        }
    }
}
