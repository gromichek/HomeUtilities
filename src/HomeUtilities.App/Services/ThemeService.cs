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
            app.UserAppTheme = theme switch
            {
                AppThemeChoice.Light => AppTheme.Light,
                AppThemeChoice.Dark => AppTheme.Dark,
                _ => AppTheme.Unspecified,
            };
        }
    }
}
