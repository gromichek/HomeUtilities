using System.Globalization;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

public sealed class LanguageService : ILanguageService
{
    private const string PreferenceKey = "language";

    public LanguageService()
    {
        var saved = Preferences.Default.Get(PreferenceKey, string.Empty);
        CurrentLanguage = SupportedLanguages.Contains(saved) ? saved : DetectSystemLanguage();
        Apply(CurrentLanguage);
    }

    public IReadOnlyList<string> SupportedLanguages { get; } = ["en", "ru"];

    public string CurrentLanguage { get; private set; }

    public event EventHandler? LanguageChanged;

    public void SetLanguage(string code)
    {
        if (!SupportedLanguages.Contains(code))
        {
            throw new ArgumentException($"Unsupported language '{code}'.", nameof(code));
        }

        CurrentLanguage = code;
        Apply(code);
        Preferences.Default.Set(PreferenceKey, code);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string DetectSystemLanguage() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";

    private static void Apply(string code)
    {
        var culture = new CultureInfo(code);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
