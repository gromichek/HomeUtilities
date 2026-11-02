using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

public sealed class ThemeService : IThemeService
{
    private const string PreferenceKey = "theme";

    public ThemeService()
    {
        var saved = Preferences.Default.Get(PreferenceKey, nameof(AppThemeChoice.System));
        Current = Enum.TryParse<AppThemeChoice>(saved, out var parsed) ? parsed : AppThemeChoice.System;
    }

    public AppThemeChoice Current { get; private set; }

    public void Set(AppThemeChoice theme)
    {
        Current = theme;
        Preferences.Default.Set(PreferenceKey, theme.ToString());
        if (Application.Current is { } app)
        {
            var target = theme switch
            {
                AppThemeChoice.Light => AppTheme.Light,
                AppThemeChoice.Dark => AppTheme.Dark,
                _ => AppTheme.Unspecified,
            };
            app.UserAppTheme = target;
#if WINDOWS
            // Windows: on the first change MAUI's texts switch, but the native (WinUI) backgrounds - menu pane, page, pickers -
            // keep the old theme; choosing the theme a second time fixes it (owner, 2026-11-02, also after the immediate
            // re-apply of #174). MAUI applies its own theme to the window while handling the change, so the native theme
            // is set again a moment later, like the owner's second choice does.
            ApplyNativeTheme(app, target);
            app.Dispatcher.Dispatch(() => ApplyNativeTheme(app, target));
            app.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () => ApplyNativeTheme(app, target));
#endif
        }
    }

#if WINDOWS
    private static void ApplyNativeTheme(Application app, AppTheme theme)
    {
        var native = theme switch
        {
            AppTheme.Light => Microsoft.UI.Xaml.ElementTheme.Light,
            AppTheme.Dark => Microsoft.UI.Xaml.ElementTheme.Dark,
            _ => Microsoft.UI.Xaml.ElementTheme.Default,
        };
        foreach (var window in app.Windows)
        {
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window { Content: Microsoft.UI.Xaml.FrameworkElement root })
            {
                root.RequestedTheme = native;
            }
        }
    }
#endif
}
