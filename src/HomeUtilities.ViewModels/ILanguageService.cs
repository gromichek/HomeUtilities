namespace HomeUtilities.ViewModels;

public interface ILanguageService
{
    IReadOnlyList<string> SupportedLanguages { get; }

    string CurrentLanguage { get; }

    event EventHandler? LanguageChanged;

    void SetLanguage(string code);
}
