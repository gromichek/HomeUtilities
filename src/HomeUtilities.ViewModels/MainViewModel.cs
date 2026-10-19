using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;

namespace HomeUtilities.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ILocalizer _localizer;
    private readonly IClock _clock;
    private readonly ILanguageService _languageService;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _todayText;

    [ObservableProperty]
    private string _hint;

    [ObservableProperty]
    private string _switchLanguageText;

    public MainViewModel(ILocalizer localizer, IClock clock, ILanguageService languageService)
    {
        _localizer = localizer;
        _clock = clock;
        _languageService = languageService;

        _title = string.Empty;
        _todayText = string.Empty;
        _hint = string.Empty;
        _switchLanguageText = string.Empty;
        RefreshTexts();

        _languageService.LanguageChanged += (_, _) => RefreshTexts();
    }

    public DateOnly Today => _clock.Today;

    [RelayCommand]
    private void ToggleLanguage()
    {
        _languageService.SetLanguage(_languageService.CurrentLanguage == "en" ? "ru" : "en");
    }

    private void RefreshTexts()
    {
        Title = _localizer["AppTitle"];
        TodayText = _localizer.Get("TodayIs", Today);
        Hint = _localizer["StartPageHint"];
        SwitchLanguageText = _localizer["SwitchLanguage"];
    }
}
