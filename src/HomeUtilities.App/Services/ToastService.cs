using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

/// <summary>
/// Short non-blocking confirmation ("Copied"). A toast is a convenience only: it must never crash the app.
/// </summary>
public sealed class ToastService : IToastService
{
    public async Task ShowAsync(string message)
    {
#if WINDOWS
        // CommunityToolkit's Toast uses Windows app notifications, which are not available to our
        // unpackaged Windows app and throw. An in-app snackbar replaces this later (see ROADMAP, T-019).
        await Task.CompletedTask;
#else
        try
        {
            await Toast.Make(message, ToastDuration.Short).Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Toast failed: {ex}");
        }
#endif
    }
}
