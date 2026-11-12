using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Overview;
using HomeUtilities.Core.Reports;
using HomeUtilities.Core.Sync;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The start screen: what to do now (readings, bills to pay or enter, verifications, expected settlements), where the money
/// stands per property and what this year cost against the same months of last year, three charts (this year by month, spending by category, consumption). It also opens the database once at the
/// start of the app (migration and seeding). The data is loaded in one go on every appearing and after a quick payment.
/// </summary>
public sealed partial class OverviewViewModel : ViewModelBase, IRecipient<LayoutModeChangedMessage>, IRecipient<SyncStatusChangedMessage>
{
    /// <summary>Settings key of the remembered property filter.</summary>
    public const string PropertyFilterKey = "overview.property";

    private readonly ILocalizer _localizer;
    private readonly IClock _clock;
    private readonly IDatabaseInitializer _databaseInitializer;
    private readonly IOverviewDataLoader _loader;
    private readonly IBillTemplateStore _templateStore;
    private readonly QuickPayService _quickPay;
    private readonly ISnackbarService _snackbar;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly ISettingsStore _settings;
    private readonly ILayoutModeService _layoutModeService;
    private readonly ReloadGate? _gate;
    private readonly ReportFilterState? _reportState;

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
    private IReadOnlyList<OverviewMoneyBlockViewModel> _moneyBlocks = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasYear), nameof(ShowYearEmpty))]
    private IReadOnlyList<OverviewBlockViewModel> _yearBlocks = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTodoEmpty), nameof(ShowMoneyEmpty), nameof(ShowYearEmpty), nameof(ShowCategoriesEmpty))]
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
        IBillTemplateStore templateStore,
        QuickPayService quickPay,
        ISnackbarService snackbar,
        INavigationService navigation,
        IDialogService dialogs,
        ISettingsStore settings,
        ILayoutModeService layoutModeService,
        IDataChangeNotifier? changes = null,
        ISyncEngine? sync = null,
        IUiDispatcher? dispatcher = null,
        ReportFilterState? reportState = null)
    {
        _reportState = reportState;
        _sync = sync;
        _dispatcher = dispatcher;

        _gate = changes is null ? null : new ReloadGate(changes, clock);
        _localizer = localizer;
        _clock = clock;
        _databaseInitializer = databaseInitializer;
        _loader = loader;
        _templateStore = templateStore;
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

    private readonly ISyncEngine? _sync;
    private readonly IUiDispatcher? _dispatcher;

    // The engine's status arrives as a weak message from SyncNotifier (M-1): no subscription to the singleton engine.
    // It is raised on the sync thread; the hint is bound to the UI (C11).
    void IRecipient<SyncStatusChangedMessage>.Receive(SyncStatusChangedMessage message)
    {
        if (_dispatcher is null)
        {
            UpdateSyncHint();
        }
        else
        {
            _dispatcher.Post(UpdateSyncHint);
        }
    }

    /// <summary>Shown only when the sync is on and has not worked for more than a day (T-031).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSyncHint))]
    private string _syncHintText = string.Empty;

    public bool HasSyncHint => SyncHintText.Length > 0;

    private void UpdateSyncHint()
    {
        var status = _sync?.Status;
        var stale = _sync is { IsEnabled: true } && status is { Kind: SyncStateKind.Failed or SyncStateKind.WaitingForNetwork or SyncStateKind.UpdateRequired }
            && _clock.UtcNow - (status.LastSyncAt ?? DateTimeOffset.MinValue) > TimeSpan.FromDays(1);
        SyncHintText = stale ? _localizer["OverviewSyncHint"] : string.Empty;
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

    protected override bool LoadingHintApplies => !IsLoaded;

    /// <summary>"Loading…" shown in place of the content while the first load takes longer than 300 ms (R-52).</summary>
    public string LoadingText => _localizer["Loading"];

    public override Task OnAppearingAsync() => RunSafeAsync(async () =>
    {
        UpdateSyncHint(); // catch up with a status change missed in between
        await InitializeDatabaseAsync();
        if (_gate is { IsFresh: true } && IsLoaded)
        {
            // Nothing was saved since the last load, same day, same language: keep what is shown (T-028h).
            PerformanceLog.ReportStartupOnce(PropertyCount);
            return;
        }

        await RunLoadAsync(ct => ReloadAsync(ct), () => PropertyCount);
        PerformanceLog.ReportStartupOnce(PropertyCount);
    });

    void IRecipient<LayoutModeChangedMessage>.Receive(LayoutModeChangedMessage message) =>
        IsWide = _layoutModeService.Current != LayoutMode.Compact;

    /// <summary>Re-reads the data and rebuilds the three cards.</summary>
    public async Task ReloadAsync(CancellationToken ct = default)
    {
        if (_databaseError.Length > 0)
        {
            return;
        }

        var today = _clock.Today;
        (long Version, DateOnly Day)? snapshot = null;
        var (data, todo, consumption) = await OffUiAsync(
            async () =>
            {
                // Drafts of the templates that came due since the last visit are created before the data is read.
                await _templateStore.GenerateDraftsAsync(today, ct);
                snapshot = _gate?.Snapshot();
                var loaded = await _loader.LoadAsync(today, ct);
                return (loaded, TodoBuilder.Build(loaded, today), OverviewCharts.Consumption(loaded, today));
            },
            ct);
        ct.ThrowIfCancellationRequested();
        _data = data;
        _todo = todo;
        _consumption = consumption;
        PropertyCount = _data.Properties.Count;
        if (_propertyFilter is { } filter && _data.Properties.All(p => p.Id != filter))
        {
            SetFilter(null);
        }

        IsLoaded = true;
        Rebuild();
        if (snapshot is { } taken)
        {
            _gate?.MarkLoaded(taken);
        }
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
        var properties = _data.Properties;
        var current = _propertyFilter is { } currentId ? properties.IndexWhere(p => p.Id == currentId) + 1 : 0;
        var picked = await _dialogs.ChooseAsync(_localizer["OverviewPropertyFilter"], [all, .. properties.Select(p => p.Name)], current);
        if (picked is not { } index)
        {
            return;
        }

        SetFilter(index == 0 ? null : properties[index - 1].Id);
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
        UpdateSyncHint();
        HelpText = _localizer["SectionHelp"];
        TodayText = _localizer.Get("TodayIs", Today);
        TodoHeader = _localizer["OverviewTodoHeader"];
        TodoEmptyText = _localizer["OverviewTodoEmpty"];
        MoneyHeader = _localizer["OverviewMoneyHeader"];
        UpdateMoneyEmptyText();
        YearHeader = _localizer["OverviewYearHeader"];
        YearEmptyText = _localizer["OverviewYearEmpty"];
        RefreshChartTexts();
        PropertyFilterLabel = _localizer["OverviewPropertyFilter"];
        RefreshDatabaseStatus();
        _gate?.Invalidate();
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
        UpdateMoneyEmptyText();
        YearBlocks = BuildYear();
        BuildCharts();
    }

    // Nothing open and no balance is "everything is paid" when bills or payments exist, and "no records" only when there are none.
    private void UpdateMoneyEmptyText() =>
        MoneyEmptyText = _localizer[IsLoaded && OverviewMoney.HasRecords(_data, _propertyFilter) ? "OverviewMoneyAllPaid" : "OverviewMoneyEmpty"];

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
            TodoKind.DraftsToConfirm => (_localizer.Get("TodoDrafts", item.Count), _localizer["TodoDraftsDetail"]),
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
            () => RunSafeAsync(() => OpenAsync(item)),
            item.BillId is { } billId && item.Kind is TodoKind.BillDue or TodoKind.TaxInstallmentDue ? () => MarkPaidAsync(billId) : null);
    }

    private string ReadingsDetail(TodoItem item, DateOnly today)
    {
        var range = ReadingsViewModel.FormatRange(_localizer, item.Date, item.EndDate ?? item.Date);
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
        TodoKind.DraftsToConfirm => OpenBillsAsync(item.PropertyId, BillStatusFilter.Drafts),
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

    private Task OpenBillsAsync(Guid propertyId, BillStatusFilter status, int? year = null, Guid? contractId = null)
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

        if (contractId is { } contract)
        {
            parameters[EditorNavigation.ContractIdParameter] = contract;
        }

        return _navigation.GoToAsync(Routes.BillsFor, parameters);
    }

    private Task OpenPaymentsAsync(Guid propertyId, Guid? contractId = null)
    {
        var parameters = new Dictionary<string, object> { [EditorNavigation.PropertyIdParameter] = propertyId };
        if (contractId is { } contract)
        {
            parameters[EditorNavigation.ContractIdParameter] = contract;
        }

        return _navigation.GoToAsync(Routes.PaymentsFor, parameters);
    }

    private Func<Task> Go(Func<Task> open) => () => RunSafeAsync(open);

    /// <summary>At most this many rows per Money section; the rest is behind a "N more" row.</summary>
    public const int MoneyRowsShown = 5;

    private IReadOnlyList<OverviewMoneyBlockViewModel> BuildMoney()
    {
        var sections = OverviewMoney.Sections(_data, _clock.Today);
        var blocks = new List<OverviewMoneyBlockViewModel>();
        foreach (var property in VisibleProperties())
        {
            if (sections.FirstOrDefault(s => s.PropertyId == property.Id) is not { } money)
            {
                continue;
            }

            var propertyId = property.Id;
            var shown = new List<OverviewMoneySectionViewModel>();

            if (money.ToPay.Count > 0)
            {
                var rows = money.ToPay.Select(r => new OverviewMoneyRowViewModel(
                    ContractName(r),
                    Money(r.Amount, r.Currency),
                    r.OverdueSince is { } since
                        ? _localizer.Get("OverviewMoneyOverdueSince", Day(since))
                        : r.NextDue is { } due ? _localizer.Get("OverviewMoneyDueOn", Day(due)) : string.Empty,
                    r.OverdueSince is null ? OverviewTone.Neutral : OverviewTone.Debt,
                    Go(() => OpenBillsAsync(propertyId, BillStatusFilter.Open, contractId: r.ContractId)))).ToList();
                shown.Add(Section("OverviewMoneyToPay", money.ToPay, rows, () => OpenBillsAsync(propertyId, BillStatusFilter.Open)));
            }

            if (money.Overpaid.Count > 0)
            {
                var rows = money.Overpaid.Select(r => new OverviewMoneyRowViewModel(
                    ContractName(r),
                    Money(r.Amount, r.Currency),
                    string.Empty,
                    OverviewTone.Neutral,
                    Go(() => OpenPaymentsAsync(propertyId, r.ContractId)))).ToList();
                shown.Add(Section("OverviewMoneyOverpaid", money.Overpaid, rows, () => OpenPaymentsAsync(propertyId)));
            }

            if (money.Settlements.Count > 0)
            {
                var rows = money.Settlements.Select(s => new OverviewMoneyRowViewModel(
                    s.Title,
                    SignedMoney(s.Expected, s.Currency),
                    _localizer[s.Expected >= 0 ? "OverviewMoneySurcharge" : "OverviewMoneyRefund"],
                    OverviewTone.Neutral,
                    Go(() => OpenSettlementAsync(s.ContractId)))).ToList();

                // The "N more" row has no list of its own to open; the first settlement not shown is the simplest existing target.
                var firstHidden = money.Settlements.Count > MoneyRowsShown ? money.Settlements[MoneyRowsShown].ContractId : Guid.Empty;
                shown.Add(new OverviewMoneySectionViewModel(
                    _localizer["OverviewMoneySettlement"],
                    string.Empty,
                    Limit(rows, () => OpenSettlementAsync(firstHidden))));
            }

            blocks.Add(new OverviewMoneyBlockViewModel(property.Name, shown));
        }

        return blocks;
    }

    private string ContractName(MoneyRow row) => row.Title ?? _localizer["OverviewMoneyNoContract"];

    // A section total only when all rows share one currency (the amounts of two currencies cannot be added).
    private OverviewMoneySectionViewModel Section(string headerKey, IReadOnlyList<MoneyRow> source, List<OverviewMoneyRowViewModel> rows, Func<Task> openAll)
    {
        var currencies = source.Select(r => r.Currency).Distinct().ToList();
        var total = currencies.Count == 1 ? Money(source.Sum(r => r.Amount), currencies[0]) : string.Empty;
        return new OverviewMoneySectionViewModel(_localizer[headerKey], total, Limit(rows, openAll));
    }

    private List<OverviewMoneyRowViewModel> Limit(List<OverviewMoneyRowViewModel> rows, Func<Task> openAll)
    {
        if (rows.Count <= MoneyRowsShown)
        {
            return rows;
        }

        var more = new OverviewMoneyRowViewModel(
            _localizer.Get("OverviewMoneyMore", rows.Count - MoneyRowsShown), string.Empty, string.Empty, OverviewTone.Neutral, Go(openAll));
        return [.. rows.Take(MoneyRowsShown), more];
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
                    var text = !c.HasLastYear
                        ? _localizer.Get("OverviewYearLineNoPrevious", c.Year, Money(c.ThisYear, c.Currency))
                        : c.ChangePercent is { } percent
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
