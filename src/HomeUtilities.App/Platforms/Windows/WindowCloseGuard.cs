using HomeUtilities.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace HomeUtilities.App.WinUI;

/// <summary>Asks the "Save changes?" question when the window is closed (✕) with unsaved input.</summary>
internal static class WindowCloseGuard
{
    public static void Attach(Window window)
    {
        var closeConfirmed = false;
        window.AppWindow.Closing += async (_, args) =>
        {
            if (closeConfirmed)
            {
                return;
            }

            var guard = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services.GetService<INavigationGuard>();
            if (guard is null)
            {
                return;
            }

            // Keep the window open while the dialog is shown, then close it again if the user agreed.
            args.Cancel = true;
            if (await guard.CanLeaveAsync())
            {
                closeConfirmed = true;
                window.Close();
            }
        };
    }
}
