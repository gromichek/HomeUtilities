using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;

namespace HomeUtilities.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ILocalizer _localizer;
    private readonly IClock _clock;
    private readonly ILanguageService _languageService;
    private readonly IDatabaseInitializer _databaseInitializer;
    private readonly ICategoryStore _categoryStore;
    private readonly IVatRateStore _vatRateStore;

    private DatabaseState _databaseState;
    private int _categoryCount;
    private int _vatRateCount;
    private string _databaseError = string.Empty;
    private bool _initializationStarted;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _todayText;

    [ObservableProperty]
    private string _hint;

    [ObservableProperty]
    private string _switchLanguageText;

    [ObservableProperty]
    private string _databaseStatusText;

    public MainViewModel(
        ILocalizer localizer,
        IClock clock,
        ILanguageService languageService,
        IDatabaseInitializer databaseInitializer,
        ICategoryStore categoryStore,
        IVatRateStore vatRateStore)
    {
        _localizer = localizer;
        _clock = clock;
        _languageService = languageService;
        _databaseInitializer = databaseInitializer;
        _categoryStore = categoryStore;
        _vatRateStore = vatRateStore;

        _title = string.Empty;
        _todayText = string.Empty;
        _hint = string.Empty;
        _switchLanguageText = string.Empty;
        _databaseStatusText = string.Empty;
        RefreshTexts();

        _languageService.LanguageChanged += (_, _) => RefreshTexts();
    }

    private enum DatabaseState
    {
        NotStarted,
        Starting,
        Ready,
        Failed,
    }

    public DateOnly Today => _clock.Today;

    /// <summary>Opens the database once (migration and seeding) and shows the result in the status line.</summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (_initializationStarted)
        {
            return;
        }

        _initializationStarted = true;
        SetDatabaseState(DatabaseState.Starting);
        try
        {
            await _databaseInitializer.InitializeAsync();
            _categoryCount = (await _categoryStore.ListAsync()).Count;
            _vatRateCount = (await _vatRateStore.ListAsync()).Count;
            SetDatabaseState(DatabaseState.Ready);
        }
        catch (Exception ex)
        {
            _databaseError = ex.Message;
            SetDatabaseState(DatabaseState.Failed);
        }
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        _languageService.SetLanguage(_languageService.CurrentLanguage == "en" ? "ru" : "en");
    }

    private void SetDatabaseState(DatabaseState state)
    {
        _databaseState = state;
        RefreshDatabaseStatus();
    }

    private void RefreshDatabaseStatus() =>
        DatabaseStatusText = _databaseState switch
        {
            DatabaseState.Starting => _localizer["DatabaseStarting"],
            DatabaseState.Ready => _localizer.Get("DatabaseReady", _categoryCount, _vatRateCount),
            DatabaseState.Failed => _localizer.Get("DatabaseError", _databaseError),
            _ => string.Empty,
        };

    private void RefreshTexts()
    {
        Title = _localizer["AppTitle"];
        TodayText = _localizer.Get("TodayIs", Today);
        Hint = _localizer["StartPageHint"];
        SwitchLanguageText = _localizer["SwitchLanguage"];
        RefreshDatabaseStatus();
    }
}
