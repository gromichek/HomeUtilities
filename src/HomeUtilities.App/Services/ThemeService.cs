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
            // Windows: the first change is applied to the MAUI resources but the native window root (WinUI) keeps its
            // old RequestedTheme until a second change. Set it explicitly, now and once more after the layout pass.
            ApplyNativeTheme(app, target);
            app.Dispatcher.Dispatch(() =>
            {
                app.UserAppTheme = target;
                ApplyNativeTheme(app, target);
            });
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
