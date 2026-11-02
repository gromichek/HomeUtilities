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
        if (Application.Current is not { } app)
        {
            return;
        }

        var target = theme switch
        {
            AppThemeChoice.Light => AppTheme.Light,
            AppThemeChoice.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };
        // Applied after the current UI event has finished: the choice comes from a Picker, and on Windows its drop-down is
        // still open (a separate popup root) while SelectionChanged runs. Switching the theme at that moment re-themes the
        // page but can leave the native parts on the old resources; one dispatch later the popup is closed.
        void Apply()
        {
            Log("before", app);
            app.UserAppTheme = target;
            Log("after", app);
        }

        if (app.Dispatcher.IsDispatchRequired || app.Windows.Count > 0)
        {
            app.Dispatcher.Dispatch(Apply);
        }
        else
        {
            Apply(); // startup: no window yet, nothing to wait for
        }
    }

    // Debug only (T-028i): the values MAUI reports after a switch; removed from Release builds by the attribute.
    [System.Diagnostics.Conditional("DEBUG")]
    private void Log(string moment, Application app) =>
        _errorLog?.Note("Theme", $"{moment}: choice={Current}, UserAppTheme={app.UserAppTheme}, RequestedTheme={app.RequestedTheme}");
}
