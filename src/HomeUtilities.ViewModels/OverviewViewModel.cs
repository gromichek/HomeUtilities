using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;

namespace HomeUtilities.ViewModels;

public partial class OverviewViewModel : ViewModelBase
{
    private readonly ILocalizer _localizer;
    private readonly IClock _clock;
    private readonly IDatabaseInitializer _databaseInitializer;
    private readonly ICategoryStore _categoryStore;
    private readonly IVatRateStore _vatRateStore;

    private DatabaseState _databaseState;
    private int _categoryCount;
    private int _vatRateCount;
    private string _databaseError = string.Empty;
    private bool _initializationStarted;

    [ObservableProperty]
    private string _todayText;

    [ObservableProperty]
    private string _hint;

    [ObservableProperty]
    private string _databaseStatusText;

    public OverviewViewModel(
        ILocalizer localizer,
        IClock clock,
        IDatabaseInitializer databaseInitializer,
        ICategoryStore categoryStore,
        IVatRateStore vatRateStore)
    {
        _localizer = localizer;
        _clock = clock;
        _databaseInitializer = databaseInitializer;
        _categoryStore = categoryStore;
        _vatRateStore = vatRateStore;

        _todayText = string.Empty;
        _hint = string.Empty;
        _databaseStatusText = string.Empty;
        RefreshTexts();
    }

    private enum DatabaseState
    {
        NotStarted,
        Starting,
        Ready,
        Failed,
    }

    public DateOnly Today => _clock.Today;

    public override Task OnAppearingAsync() => InitializeAsync();

    /// <summary>Opens the database once (migration and seeding) and shows the result in the status line.</summary>
    [RelayCommand]
    private Task InitializeAsync() => RunSafeAsync(async () =>
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
    });

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

    protected override void RefreshTexts()
    {
        Title = _localizer["AppTitle"];
        TodayText = _localizer.Get("TodayIs", Today);
        Hint = _localizer["StartPageHint"];
        RefreshDatabaseStatus();
    }
}
