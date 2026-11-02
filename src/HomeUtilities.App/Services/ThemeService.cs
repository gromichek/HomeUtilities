using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

public sealed class ThemeService : IThemeService
{
    private const string PreferenceKey = "theme";

    private readonly IErrorLog? _errorLog;

    public ThemeService(IErrorLog? errorLog = null)
    {
        _errorLog = errorLog;
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
            ApplyNativeTheme(app, target);
#endif
        }
    }

#if WINDOWS
    /// <summary>
    /// Windows (T-028h): MAUI only sets the root element's <c>RequestedTheme</c>; WinUI re-resolves the theme resources of the
    /// native parts (navigation pane, page background, pickers) only when the <b>effective</b> theme of the element tree really
    /// changes. Re-applying the same value (the attempts of #174 and #177) does nothing, and the first choice from "System" can
    /// leave those parts on the old resources, while the second choice - a real change - fixed them. So the root is first
    /// flipped to the opposite theme and then set to the wanted one, which always makes WinUI re-resolve everything.
    /// </summary>
    private void ApplyNativeTheme(Application app, AppTheme theme)
    {
        var wanted = theme switch
        {
            AppTheme.Light => Microsoft.UI.Xaml.ElementTheme.Light,
            AppTheme.Dark => Microsoft.UI.Xaml.ElementTheme.Dark,
            _ => Microsoft.UI.Xaml.ElementTheme.Default,
        };
        var opposite = wanted switch
        {
            Microsoft.UI.Xaml.ElementTheme.Light => Microsoft.UI.Xaml.ElementTheme.Dark,
            Microsoft.UI.Xaml.ElementTheme.Dark => Microsoft.UI.Xaml.ElementTheme.Light,
            _ => SystemIsDark() ? Microsoft.UI.Xaml.ElementTheme.Light : Microsoft.UI.Xaml.ElementTheme.Dark,
        };
        foreach (var window in app.Windows)
        {
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window { Content: Microsoft.UI.Xaml.FrameworkElement root })
            {
                Log("before", app, root);
                root.RequestedTheme = opposite;
                root.UpdateLayout();
                root.RequestedTheme = wanted;
                root.UpdateLayout();
                Log("after", app, root);
            }
        }
    }

    private static bool SystemIsDark()
    {
        var background = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
        return background.R + background.G + background.B < 3 * 128;
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void Log(string moment, Application app, Microsoft.UI.Xaml.FrameworkElement root) =>
        _errorLog?.Note(
            "Theme",
            $"{moment}: choice={Current}, UserAppTheme={app.UserAppTheme}, RequestedTheme={app.RequestedTheme}, root.RequestedTheme={root.RequestedTheme}, root.ActualTheme={root.ActualTheme}");
#endif
}
