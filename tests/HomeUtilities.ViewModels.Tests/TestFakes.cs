using CommunityToolkit.Mvvm.Messaging;

namespace HomeUtilities.ViewModels.Tests;

internal sealed class FakeLanguageService : ILanguageService
{
    public IReadOnlyList<string> SupportedLanguages { get; } = ["en", "ru"];

    public string CurrentLanguage { get; private set; } = "en";

    public List<string> SetCalls { get; } = [];

    public void SetLanguage(string code)
    {
        SetCalls.Add(code);
        CurrentLanguage = code;
        WeakReferenceMessenger.Default.Send(new LanguageChangedMessage(code));
    }
}

internal sealed class FakeLocalizer(FakeLanguageService language) : ILocalizer
{
    public string this[string key] => $"{key} [{language.CurrentLanguage}]";

    public string Get(string key, params object[] args) =>
        $"{key} {string.Join("|", args)} [{language.CurrentLanguage}]";
}

internal sealed class FakeLayoutModeService : ILayoutModeService
{
    public LayoutMode Current { get; private set; } = LayoutMode.Compact;

    public void Change(LayoutMode mode)
    {
        Current = mode;
        WeakReferenceMessenger.Default.Send(new LayoutModeChangedMessage(mode));
    }
}

internal sealed class FakeThemeService : IThemeService
{
    public AppThemeChoice Current { get; private set; } = AppThemeChoice.System;

    public List<AppThemeChoice> SetCalls { get; } = [];

    public void Set(AppThemeChoice theme)
    {
        SetCalls.Add(theme);
        Current = theme;
    }
}
