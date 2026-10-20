namespace HomeUtilities.ViewModels;

public interface ILanguageService
{
    IReadOnlyList<string> SupportedLanguages { get; }

    string CurrentLanguage { get; }

    /// <summary>Applies the language and sends <see cref="LanguageChangedMessage"/>.</summary>
    void SetLanguage(string code);
}
