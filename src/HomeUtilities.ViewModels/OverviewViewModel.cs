using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Overview;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The start screen: what to do now (readings, bills to pay or enter, verifications, expected settlements), where the money
/// stands per property and what this year cost against the same months of last year. It also opens the database once at the
/// start of the app (migration and seeding). The data is loaded in one go on every appearing and after a quick payment.
/// </summary>
public sealed partial class OverviewViewModel : ViewModelBase, IRecipient<LayoutModeChangedMessage>
{
    /// <summary>Settings key of the remembered property filter.</summary>
    public const string PropertyFilterKey = "overview.property";

    private readonly ILocalizer _localizer;
    private readonly IClock _clock;
    private readonly IDatabaseInitializer _databaseInitializer;
    private readonly IOverviewDataLoader _loader;
    private readonly QuickPayService _quickPay;
    private readonly ISnackbarService _snackbar;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly ISettingsStore _settings;
    private readonly ILayoutModeService _layoutModeService;

    private OverviewData _data = OverviewData.Empty;
    private IReadOnlyList<TodoItem> _todo = [];
    private Guid? _propertyFilter;
    private string _databaseError = string.Empty;
    private bool _initializationStarted;

    [ObservableProperty]
    private string _todayText = string.Empty;

    [ObservableProperty]
    private string _helpText = string.Empty;

    [ObservableProperty]
    private string _databaseStatusText = string.Empty;

    [ObservableProperty]
    private string _todoHeader = string.Empty;

    [ObservableProperty]
    private string _todoEmptyText = string.Empty;

    [ObservableProperty]
    private string _moneyHeader = string.Empty;

    [ObservableProperty]
    private string _moneyEmptyText = string.Empty;

    [ObservableProperty]
    private string _yearHeader = string.Empty;

    [ObservableProperty]
    private string _yearEmptyText = string.Empty;

    [ObservableProperty]
    private string _propertyFilterLabel = string.Empty;

    [ObservableProperty]
    private string _propertyFilterName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTodo), nameof(ShowTodoEmpty))]
    private IReadOnlyList<TodoRowViewModel> _todoRows = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMoney), nameof(ShowMoneyEmpty))]
    private IReadOnlyList<OverviewBlockViewModel> _moneyBlocks = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasYear), nameof(ShowYearEmpty))]
    private IReadOnlyList<OverviewBlockViewModel> _yearBlocks = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTodoEmpty), nameof(ShowMoneyEmpty), nameof(ShowYearEmpty))]
    private bool _isLoaded;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCompact))]
    private bool _isWide;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPropertyFilter))]
    private int _propertyCount;

    public OverviewViewModel(
        ILocalizer localizer,
        IClock clock,
        IDatabaseInitializer databaseInitializer,
        IOverviewDataLoader loader,
        QuickPayService quickPay,
        ISnackbarService snackbar,
        INavigationService navigation,
        IDialogService dialogs,
        ISettingsStore settings,
        ILayoutModeService layoutModeService)
    {
        _localizer = localizer;
        _clock = clock;
        _databaseInitializer = databaseInitializer;
        _loader = loader;
        _quickPay = quickPay;
        _snackbar = snackbar;
        _navigation = navigation;
        _dialogs = dialogs;
        _settings = settings;
        _layoutModeService = layoutModeService;
        _isWide = layoutModeService.Current != LayoutMode.Compact;
        _propertyFilter = Guid.TryParse(settings.GetString(PropertyFilterKey), out var remembered) ? remembered : null;
        RefreshTexts();
    }

    public DateOnly Today => _clock.Today;

    public bool IsCompact => !IsWide;

    public bool HasTodo => TodoRows.Count > 0;

    public bool ShowTodoEmpty => IsLoaded && TodoRows.Count == 0;

    public bool HasMoney => MoneyBlocks.Count > 0;

    public bool ShowMoneyEmpty => IsLoaded && MoneyBlocks.Count == 0;

    public bool HasYear => YearBlocks.Count > 0;

    public bool ShowYearEmpty => IsLoaded && YearBlocks.Count == 0;

    /// <summary>The property picker is shown only when there is something to choose from.</summary>
    public bool ShowPropertyFilter => PropertyCount > 1;

    /// <summary>The remembered property filter; null = all properties.</summary>
    public Guid? PropertyFilterId => _propertyFilter;

    public override Task OnAppearingAsync() => RunSafeAsync(async () =>
    {
        await InitializeDatabaseAsync();
        await ReloadAsync();
    });

    void IRecipient<LayoutModeChangedMessage>.Receive(LayoutModeChangedMessage message) =>
        IsWide = _layoutModeService.Current != LayoutMode.Compact;

    /// <summary>Re-reads the data and rebuilds the three cards.</summary>
    public async Task ReloadAsync()
    {
        if (_databaseError.Length > 0)
        {
            return;
        }

        _data = await _loader.LoadAsync(_clock.Today);
        _todo = TodoBuilder.Build(_data, _clock.Today);
        PropertyCount = _data.Properties.Count;
        if (_propertyFilter is { } filter && _data.Properties.All(p => p.Id != filter))
        {
            SetFilter(null);
        }

        IsLoaded = true;
        Rebuild();
    }

    /// <summary>Pull-to-refresh.</summary>
    [RelayCommand]
    private Task RefreshAsync() => RunSafeAsync(async () =>
    {
        IsRefreshing = true;
        try
        {
            await ReloadAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    });

    [RelayCommand]
    private Task OpenHelpAsync() => RunSafeAsync(() => HelpNavigation.OpenAsync(_navigation, HelpTopic.Overview));

    [RelayCommand]
    private Task ChoosePropertyFilterAsync() => RunSafeAsync(async () =>
    {
        var all = _localizer["BillAllProperties"];
        var picked = await _dialogs.ChooseAsync(_localizer["OverviewPropertyFilter"], [all, .. _data.Properties.Select(p => p.Name)]);
        if (picked is null)
        {
            return;
        }

        SetFilter(picked == all ? null : _data.Properties.FirstOrDefault(p => p.Name == picked)?.Id);
        Rebuild();
    });

    private void SetFilter(Guid? propertyId)
    {
        _propertyFilter = propertyId;
        _settings.SetString(PropertyFilterKey, propertyId?.ToString());
    }

    /// <summary>The "Paid" quick action of a to-do row: pays the bill, reloads, then "Bill marked as paid · Undo".</summary>
    public Task MarkPaidAsync(Guid billId) => RunSafeAsync(async () =>
    {
        var payment = await _quickPay.MarkPaidAsync(billId);
        await ReloadAsync();
        if (payment is null)
        {
            return;
        }

        if (await _snackbar.ShowAsync(_localizer["BillMarkedPaid"], _localizer["BillUndo"]))
        {
            await _quickPay.UndoAsync(payment.Id);
            await ReloadAsync();
        }
    });

    private async Task InitializeDatabaseAsync()
    {
        if (_initializationStarted)
        {
            return;
        }

        _initializationStarted = true;
        try
        {
            await _databaseInitializer.InitializeAsync();
        }
        catch (Exception ex)
        {
            _databaseError = ex.Message;
            RefreshDatabaseStatus();
        }
    }

    private void RefreshDatabaseStatus() =>
        DatabaseStatusText = _databaseError.Length > 0 ? _localizer.Get("DatabaseError", _databaseError) : string.Empty;

    protected override void RefreshTexts()
    {
        Title = _localizer["SectionOverview"];
        HelpText = _localizer["SectionHelp"];
        TodayText = _localizer.Get("TodayIs", Today);
        TodoHeader = _localizer["OverviewTodoHeader"];
        TodoEmptyText = _localizer["OverviewTodoEmpty"];
        MoneyHeader = _localizer["OverviewMoneyHeader"];
        MoneyEmptyText = _localizer["OverviewMoneyEmpty"];
        YearHeader = _localizer["OverviewYearHeader"];
        YearEmptyText = _localizer["OverviewYearEmpty"];
        PropertyFilterLabel = _localizer["OverviewPropertyFilter"];
        RefreshDatabaseStatus();
        if (IsLoaded)
        {
            Rebuild();
        }
    }

    // ---- Cards ----------------------------------------------------------------------------------------------------

    private void Rebuild()
    {
        PropertyFilterName = _propertyFilter is { } id
            ? _data.Properties.FirstOrDefault(p => p.Id == id)?.Name ?? _localizer["BillAllProperties"]
            : _localizer["BillAllProperties"];
        TodoRows = [.. _todo.Where(Matches).Select(CreateRow)];
        MoneyBlocks = BuildMoney();
        YearBlocks = BuildYear();
    }

    private bool Matches(TodoItem item) => _propertyFilter is not { } filter || item.PropertyId == filter;

    private IEnumerable<Property> VisibleProperties() =>
        _data.Properties.Where(p => _propertyFilter is not { } filter || p.Id == filter);

    private string NameOf(Guid propertyId) => _data.Properties.FirstOrDefault(p => p.Id == propertyId)?.Name ?? string.Empty;

    private string Money(decimal amount, Currency currency) => BillText.Money(amount, _localizer["Currency_Symbol_" + currency]);

    private string SignedMoney(decimal amount, Currency currency) => BillText.SignedMoney(amount, _localizer["Currency_Symbol_" + currency]);

    private static string Day(DateOnly date) => BillText.Date(date);

    private TodoRowViewModel CreateRow(TodoItem item)
    {
        var today = _clock.Today;
        var property = NameOf(item.PropertyId);
        var (text, detail) = item.Kind switch
        {
            TodoKind.ReadingsWindow => (_localizer.Get("TodoReadings", string.Join(", ", item.Args)), ReadingsDetail(item, today)),
            TodoKind.BillDue => (_localizer.Get("TodoBillDue", item.Args[0], BillText.Period(item.Period!.Value)), DueDetail(item, today)),
            TodoKind.TaxInstallmentDue => (_localizer.Get("TodoTaxDue", item.Args[0]), DueDetail(item, today)),
            TodoKind.BillMissing => (
                _localizer.Get("TodoBillMissing", BillText.Period(item.Period!.Value), item.Args[0]),
                _localizer.Get("TodoBillMissingDetail", Day(item.Date))),
            TodoKind.VerificationDue => (_localizer.Get("TodoVerification", item.Args[0]), VerificationDetail(item, today)),
            _ => (
                _localizer.Get("TodoSettlement", item.Args[0]),
                _localizer[item.Amount > 0 ? "TodoSettlementSurcharge" : "TodoSettlementRefund"]),
        };

        var amount = item.Kind switch
        {
            TodoKind.BillDue or TodoKind.TaxInstallmentDue => Money(item.Amount ?? 0m, item.Currency),
            TodoKind.SettlementExpected => SignedMoney(item.Amount ?? 0m, item.Currency),
            _ => string.Empty,
        };

        return new TodoRowViewModel(
            item,
            text,
            $"{property} · {detail}",
            amount,
            _localizer["BillMarkPaid"],
            _localizer["TodoEnterBill"],
            () => _ = RunSafeAsync(() => OpenAsync(item)),
            item.BillId is { } billId && item.Kind is TodoKind.BillDue or TodoKind.TaxInstallmentDue ? () => _ = MarkPaidAsync(billId) : null);
    }

    private string ReadingsDetail(TodoItem item, DateOnly today)
    {
        var range = ReadingsViewModel.FormatRange(item.Date, item.EndDate ?? item.Date);
        switch (item.Severity)
        {
            case TodoSeverity.Today:
                return _localizer.Get("TodoReadingsOpen", range);
            case TodoSeverity.Soon:
                var days = item.Date.DayNumber - today.DayNumber;
                return days == 1 ? _localizer.Get("TodoReadingsOpensTomorrow", range) : _localizer.Get("TodoReadingsOpensIn", range, days);
            default:
                return _localizer.Get("TodoReadingsMissed", range);
        }
    }

    private string DueDetail(TodoItem item, DateOnly today)
    {
        var days = Math.Abs(item.Date.DayNumber - today.DayNumber);
        return item.Severity switch
        {
            TodoSeverity.Overdue => _localizer.Get("TodoDueOverdue", Day(item.Date), days),
            TodoSeverity.Today => _localizer["TodoDueToday"],
            _ when days == 1 => _localizer.Get("TodoDueTomorrow", Day(item.Date)),
            _ => _localizer.Get("TodoDueIn", Day(item.Date), days),
        };
    }

    private string VerificationDetail(TodoItem item, DateOnly today) =>
        item.Severity == TodoSeverity.Overdue
            ? _localizer.Get("TodoVerificationOverdue", Day(item.Date))
            : _localizer.Get("TodoVerificationDue", Day(item.Date), item.Date.DayNumber - today.DayNumber);

    private Task OpenAsync(TodoItem item) => item.Kind switch
    {
        TodoKind.ReadingsWindow => _navigation.GoToAsync(
            Routes.ReadingsFor,
            new Dictionary<string, object>
            {
                [EditorNavigation.PropertyIdParameter] = item.PropertyId,
                [ReadingsViewModel.DateParameter] = _clock.Today,
            }),
        TodoKind.BillDue or TodoKind.TaxInstallmentDue => _navigation.GoToAsync(
            Routes.BillEdit,
            new Dictionary<string, object> { [EditorNavigation.IdParameter] = item.BillId!.Value }),
        TodoKind.BillMissing => _navigation.GoToAsync(
            Routes.BillEdit,
            new Dictionary<string, object>
            {
                [EditorNavigation.ContractIdParameter] = item.ContractId!.Value,
                [EditorNavigation.PropertyIdParameter] = item.PropertyId,
                [BillEditViewModel.PeriodParameter] = item.Period!.Value.ToString(),
            }),
        TodoKind.VerificationDue => _navigation.GoToAsync(
            Routes.MeterEdit,
            new Dictionary<string, object>
            {
                [EditorNavigation.IdParameter] = item.MeterId!.Value,
                [EditorNavigation.PropertyIdParameter] = item.PropertyId,
            }),
        _ => OpenSettlementAsync(item.ContractId!.Value),
    };

    private Task OpenSettlementAsync(Guid contractId) => _navigation.GoToAsync(
        Routes.EqualizedSettlement,
        new Dictionary<string, object> { [EditorNavigation.ContractIdParameter] = contractId });

    private Task OpenBillsAsync(Guid propertyId, BillStatusFilter status, int? year = null)
    {
        var parameters = new Dictionary<string, object>
        {
            [EditorNavigation.PropertyIdParameter] = propertyId,
            [BillsViewModel.StatusParameter] = status.ToString(),
        };
        if (year is { } shown)
        {
            parameters[BillsViewModel.YearParameter] = shown;
        }

        return _navigation.GoToAsync(Routes.BillsFor, parameters);
    }

    private Task OpenPaymentsAsync(Guid propertyId) => _navigation.GoToAsync(
        Routes.PaymentsFor,
        new Dictionary<string, object> { [EditorNavigation.PropertyIdParameter] = propertyId });

    private Action Go(Func<Task> open) => () => _ = RunSafeAsync(open);

    private IReadOnlyList<OverviewBlockViewModel> BuildMoney()
    {
        var money = OverviewMoney.PerProperty(_data, _clock.Today);
        var settlements = OverviewMoney.Settlements(_data);
        var blocks = new List<OverviewBlockViewModel>();
        foreach (var property in VisibleProperties())
        {
            var lines = new List<OverviewLineViewModel>();
            foreach (var row in money.Where(m => m.PropertyId == property.Id))
            {
                var propertyId = property.Id;
                var currency = row.Currency;
                if (row.OpenBills != 0)
                {
                    lines.Add(new(_localizer.Get("OverviewOpenBills", Money(row.OpenBills, currency)), OverviewTone.Neutral, Go(() => OpenBillsAsync(propertyId, BillStatusFilter.Open))));
                }

                if (row.OverdueBills != 0)
                {
                    lines.Add(new(_localizer.Get("OverviewOverdueBills", Money(row.OverdueBills, currency)), OverviewTone.Debt, Go(() => OpenBillsAsync(propertyId, BillStatusFilter.Overdue))));
                }

                if (row.Prepayments != 0)
                {
                    lines.Add(new(_localizer.Get("OverviewPrepayments", Money(row.Prepayments, currency)), OverviewTone.Good, Go(() => OpenPaymentsAsync(propertyId))));
                }

                if (row.Balance != 0)
                {
                    lines.Add(row.Balance < 0
                        ? new(_localizer.Get("ContractBalanceDebt", Money(-row.Balance, currency)), OverviewTone.Debt, Go(() => OpenBillsAsync(propertyId, BillStatusFilter.All)))
                        : new(_localizer.Get("ContractBalanceAdvance", Money(row.Balance, currency)), OverviewTone.Good, Go(() => OpenPaymentsAsync(propertyId))));
                }
            }

            foreach (var settlement in settlements.Where(s => s.PropertyId == property.Id))
            {
                var contractId = settlement.ContractId;
                lines.Add(new(
                    _localizer.Get(
                        settlement.Expected >= 0 ? "OverviewSettlementSurcharge" : "OverviewSettlementRefund",
                        settlement.Title,
                        Money(Math.Abs(settlement.Expected), settlement.Currency)),
                    OverviewTone.Neutral,
                    Go(() => OpenSettlementAsync(contractId))));
            }

            if (lines.Count > 0)
            {
                blocks.Add(new OverviewBlockViewModel(property.Name, lines));
            }
        }

        return blocks;
    }

    private IReadOnlyList<OverviewBlockViewModel> BuildYear()
    {
        var comparisons = OverviewMoney.YearToDate(_data, _clock.Today);
        var blocks = new List<OverviewBlockViewModel>();
        foreach (var property in VisibleProperties())
        {
            var propertyId = property.Id;
            var lines = comparisons
                .Where(c => c.PropertyId == propertyId)
                .Select(c =>
                {
                    var year = c.Year;
                    var text = c.ChangePercent is { } percent
                        ? _localizer.Get("OverviewYearLine", c.Year, Money(c.ThisYear, c.Currency), c.Year - 1, Money(c.LastYear, c.Currency), BillText.SignedPercent(percent))
                        : _localizer.Get("OverviewYearLineNoChange", c.Year, Money(c.ThisYear, c.Currency), c.Year - 1, Money(c.LastYear, c.Currency));
                    return new OverviewLineViewModel(text, OverviewTone.Neutral, Go(() => OpenBillsAsync(propertyId, BillStatusFilter.All, year)));
                })
                .ToList();
            if (lines.Count > 0)
            {
                blocks.Add(new OverviewBlockViewModel(property.Name, lines));
            }
        }

        return blocks;
    }
}
