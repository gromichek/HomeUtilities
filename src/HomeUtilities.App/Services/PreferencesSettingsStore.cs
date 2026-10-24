using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

/// <summary>Per-device settings in the platform preferences. A failing storage must never crash the app.</summary>
public sealed class PreferencesSettingsStore : ISettingsStore
{
    public string? GetString(string key)
    {
        try
        {
            var value = Preferences.Default.Get(key, string.Empty);
            return value.Length == 0 ? null : value;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Reading setting '{key}' failed: {ex}");
            return null;
        }
    }

    public void SetString(string key, string? value)
    {
        try
        {
            if (value is null)
            {
                Preferences.Default.Remove(key);
            }
            else
            {
                Preferences.Default.Set(key, value);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Writing setting '{key}' failed: {ex}");
        }
    }
}
