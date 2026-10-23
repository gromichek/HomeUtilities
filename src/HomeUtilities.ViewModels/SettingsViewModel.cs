using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace HomeUtilities.ViewModels;

public sealed record LanguageOption(string Code, string DisplayName);

public sealed record ThemeOption(AppThemeChoice Theme, string DisplayName);

public sealed partial class SettingsViewModel : ViewModelBase, IRecipient<LayoutModeChangedMessage>
{
    private readonly ILocalizer _localizer;
    private readonly ILanguageService _languageService;
    private readonly IThemeService _themeService;
    private readonly ILayoutModeService _layoutModeService;
    private readonly INavigationService _navigation;
    private readonly IErrorLog _errorLog;
    private readonly IClipboardService _clipboard;
    private readonly IToastService _toast;

    private bool _refreshing;

    [ObservableProperty]
    private IReadOnlyList<LanguageOption> _languages = [];

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    [ObservableProperty]
    private IReadOnlyList<ThemeOption> _themes = [];

    [ObservableProperty]
    private ThemeOption? _selectedTheme;

    [ObservableProperty]
    private string _languageLabel = string.Empty;

    [ObservableProperty]
    private string _themeLabel = string.Empty;

    [ObservableProperty]
    private string _categoriesText = string.Empty;

    [ObservableProperty]
    private string _versionText = string.Empty;

    [ObservableProperty]
    private string _databasePathText = string.Empty;

    [ObservableProperty]
    private string _layoutText = string.Empty;

    [ObservableProperty]
    private string _errorLogHeader = string.Empty;

    [ObservableProperty]
    private string _errorLogHint = string.Empty;

    [ObservableProperty]
    private string _copyLogText = string.Empty;

    [ObservableProperty]
    private string _clearLogText = string.Empty;

    public SettingsViewModel(
        ILocalizer localizer,
        ILanguageService languageService,
        IThemeService themeService,
        ILayoutModeService layoutModeService,
        IAppInfo appInfo,
        IAppPaths appPaths,
        INavigationService navigation,
        IErrorLog errorLog,
        IClipboardService clipboard,
        IToastService toast)
    {
        _errorLog = errorLog;
        _clipboard = clipboard;
        _toast = toast;
        _localizer = localizer;
        _languageService = languageService;
        _themeService = themeService;
        _layoutModeService = layoutModeService;
        _navigation = navigation;
        Version = appInfo.Version;
        DatabasePath = appPaths.DatabasePath;
        RefreshTexts();
    }

    public string Version { get; }

    public string DatabasePath { get; }

    [RelayCommand]
    private Task OpenCategoriesAsync() => RunSafeAsync(() => _navigation.GoToAsync(Routes.Categories));

    /// <summary>Puts the error log on the clipboard so the owner can send it; an empty log says so instead.</summary>
    [RelayCommand]
    private Task CopyLogAsync() => RunSafeAsync(async () =>
    {
        var log = _errorLog.ReadAll();
        if (log.Length == 0)
        {
            await _toast.ShowAsync(_localizer["SettingsErrorLogEmpty"]);
            return;
        }

        await _clipboard.SetTextAsync(log);
        await _toast.ShowAsync(_localizer["Copied"]);
    });

    [RelayCommand]
    private void ClearLog() => _errorLog.Clear();

    void IRecipient<LayoutModeChangedMessage>.Receive(LayoutModeChangedMessage message) => RefreshLayoutText();

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (!_refreshing && value is not null && value.Code != _languageService.CurrentLanguage)
        {
            _languageService.SetLanguage(value.Code);
        }
    }

    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
        if (!_refreshing && value is not null && value.Theme != _themeService.Current)
        {
            _themeService.Set(value.Theme);
        }
    }

    protected override void RefreshTexts()
    {
        _refreshing = true;
        try
        {
            Title = _localizer["SectionSettings"];
            CategoriesText = _localizer["SectionCategories"];
            ErrorLogHeader = _localizer["SettingsErrorLog"];
            ErrorLogHint = _localizer["SettingsErrorLogHint"];
            CopyLogText = _localizer["SettingsErrorLogCopy"];
            ClearLogText = _localizer["SettingsErrorLogClear"];
            LanguageLabel = _localizer["SettingsLanguage"];
            ThemeLabel = _localizer["SettingsTheme"];
            VersionText = _localizer.Get("SettingsVersion", Version);
            DatabasePathText = _localizer.Get("SettingsDatabaseFile", DatabasePath);

            Languages = [.. _languageService.SupportedLanguages.Select(c => new LanguageOption(c, _localizer[$"LanguageName_{c}"]))];
            SelectedLanguage = Languages.FirstOrDefault(l => l.Code == _languageService.CurrentLanguage);

            Themes = [.. Enum.GetValues<AppThemeChoice>().Select(t => new ThemeOption(t, _localizer[$"Theme_{t}"]))];
            SelectedTheme = Themes.FirstOrDefault(t => t.Theme == _themeService.Current);

            RefreshLayoutText();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void RefreshLayoutText() =>
        LayoutText = _localizer.Get("SettingsLayout", _localizer[$"Layout_{_layoutModeService.Current}"]);
}
