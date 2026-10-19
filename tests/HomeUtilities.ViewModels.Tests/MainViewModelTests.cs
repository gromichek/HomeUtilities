using HomeUtilities.Core;

namespace HomeUtilities.ViewModels.Tests;

public class MainViewModelTests
{
    [Fact]
    public void Title_ComesFromLocalizer()
    {
        var vm = CreateViewModel(out _, out _);

        Assert.Equal("Home utilities [en]", vm.Title);
        Assert.Equal("Today is 2026-10-19 [en]", vm.TodayText);
    }

    [Fact]
    public void ToggleLanguage_SwitchesEnToRuToEn()
    {
        var vm = CreateViewModel(out var language, out _);

        vm.ToggleLanguageCommand.Execute(null);
        Assert.Equal("ru", language.CurrentLanguage);

        vm.ToggleLanguageCommand.Execute(null);
        Assert.Equal("en", language.CurrentLanguage);
    }

    [Fact]
    public void Title_IsRefreshedAfterLanguageChanged()
    {
        var vm = CreateViewModel(out var language, out _);

        language.SetLanguage("ru");

        Assert.Equal("Home utilities [ru]", vm.Title);
        Assert.Equal("Today is 2026-10-19 [ru]", vm.TodayText);
    }

    private static MainViewModel CreateViewModel(out FakeLanguageService language, out FakeClock clock)
    {
        language = new FakeLanguageService();
        clock = new FakeClock();
        return new MainViewModel(new FakeLocalizer(language), clock, language);
    }

    private sealed class FakeClock : IClock
    {
        public DateOnly Today => new(2026, 10, 19);

        public DateTimeOffset UtcNow => new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeLanguageService : ILanguageService
    {
        public IReadOnlyList<string> SupportedLanguages { get; } = ["en", "ru"];

        public string CurrentLanguage { get; private set; } = "en";

        public event EventHandler? LanguageChanged;

        public void SetLanguage(string code)
        {
            CurrentLanguage = code;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeLocalizer(FakeLanguageService language) : ILocalizer
    {
        public string this[string key] => key switch
        {
            "AppTitle" => $"Home utilities [{language.CurrentLanguage}]",
            _ => $"{key} [{language.CurrentLanguage}]",
        };

        public string Get(string key, params object[] args) =>
            key == "TodayIs"
                ? $"Today is {((DateOnly)args[0]):yyyy-MM-dd} [{language.CurrentLanguage}]"
                : $"{key} [{language.CurrentLanguage}]";
    }
}
