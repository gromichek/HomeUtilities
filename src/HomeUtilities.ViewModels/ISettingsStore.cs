namespace HomeUtilities.ViewModels;

/// <summary>Small per-device key/value settings (e.g. the last selected property); implemented with MAUI Preferences.</summary>
public interface ISettingsStore
{
    /// <summary>The stored text, or null if the key was never set.</summary>
    string? GetString(string key);

    /// <summary>Stores the text; null removes the key.</summary>
    void SetString(string key, string? value);
}
