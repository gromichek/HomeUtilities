using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The readings screen: pick a property, type the new value of every register, see consumption and warnings,
/// save all at once, open the supplier's submit page and mark the readings as submitted.
/// </summary>
public sealed partial class ReadingsViewModel : ViewModelBase, IRecipient<LayoutModeChangedMessage>
{
    /// <summary>Settings key of the last selected property.</summary>
    public const string LastPropertyKey = "readings.last_property";

    private readonly IPropertyStore _propertyStore;
    private readonly IMeterStore _meterStore;
    private readonly IContractStore _contractStore;
    private readonly ISupplierStore _supplierStore;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly IToastService _toast;
    private readonly ILauncherService _launcher;
    private readonly ISettingsStore _settings;
    private readonly ILayoutModeService _layoutModeService;
    private readonly Func<ReadingHistoryViewModel> _historyFactory;

    private IReadOnlyList<Meter> _meters = [];
    private IReadOnlyList<ContractInfo> _contracts = [];
    private List<(Guid MeterId, MeterReading Reading)> _lastSaved = [];
    private List<Guid> _dismissedPrompts = [];
    private Guid? _selectedPropertyId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPropertyIndex), nameof(HasMultipleProperties), nameof(HasProperties), nameof(ShowNoProperties), nameof(ShowNoMeters))]
    private IReadOnlyList<ChoiceOption> _properties = [];

    [ObservableProperty]
    private DateOnly _readingDate;

    [ObservableProperty]
    private IReadOnlyList<ReadingWindowBannerViewModel> _banners = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMeters), nameof(ShowNoMeters))]
    private IReadOnlyList<ReadingGroupViewModel> _groups = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPrompts))]
    private IReadOnlyList<SubmitPromptViewModel> _prompts = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHistoryHint))]
    private bool _isTwoPane;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedHistory), nameof(ShowHistoryHint))]
    private ReadingHistoryViewModel? _selectedHistory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoProperties), nameof(ShowNoMeters))]
    private bool _isLoaded;

    [ObservableProperty]
    private string _propertyLabel = string.Empty;

    [ObservableProperty]
    private string _dateLabel = string.Empty;

    [ObservableProperty]
    private string _saveAllText = string.Empty;

    [ObservableProperty]
    private string _noPropertiesText = string.Empty;

    [ObservableProperty]
    private string _noMetersText = string.Empty;

    [ObservableProperty]
    private string _historyHintText = string.Empty;

    public ReadingsViewModel(
        IPropertyStore propertyStore,
        IMeterStore meterStore,
        IContractStore contractStore,
        ISupplierStore supplierStore,
        IClock clock,
        ILocalizer localizer,
        IDialogService dialogs,
        INavigationService navigation,
        IToastService toast,
        ILauncherService launcher,
        ISettingsStore settings,
        ILayoutModeService layoutModeService,
        Func<ReadingHistoryViewModel> historyFactory)
    {
        _propertyStore = propertyStore;
        _meterStore = meterStore;
        _contractStore = contractStore;
        _supplierStore = supplierStore;
        _clock = clock;
        _localizer = localizer;
        _dialogs = dialogs;
        _navigation = navigation;
        _toast = toast;
        _launcher = launcher;
        _settings = settings;
        _layoutModeService = layoutModeService;
        _historyFactory = historyFactory;
        _isTwoPane = layoutModeService.Current != LayoutMode.Compact;
        _readingDate = clock.Today;
        RefreshTexts();
    }

    /// <summary>The last property change started from the picker.</summary>
    public Task PendingPropertyChange { get; private set; } = Task.CompletedTask;

    public bool HasProperties => Properties.Count > 0;

    /// <summary>The picker is shown only when there is something to choose from.</summary>
    public bool HasMultipleProperties => Properties.Count > 1;

    public bool HasMeters => Groups.Count > 0;

    public bool HasPrompts => Prompts.Count > 0;

    public bool ShowNoProperties => IsLoaded && Properties.Count == 0;

    public bool ShowNoMeters => IsLoaded && Properties.Count > 0 && Groups.Count == 0;

    public bool HasSelectedHistory => SelectedHistory is not null;

    public bool ShowHistoryHint => IsTwoPane && SelectedHistory is null;

    /// <summary>True while a new value has been typed and not saved.</summary>
    public bool HasUnsavedValues => Groups.Any(g => g.Cards.Any(c => c.Rows.Any(r => r.HasInput)));

    public Guid? SelectedPropertyId => _selectedPropertyId;

    public int SelectedPropertyIndex
    {
        get => Properties.ToList().FindIndex(p => p.Id == _selectedPropertyId);
        set
        {
            if (value < 0 || value >= Properties.Count || Properties[value].Id == _selectedPropertyId)
            {
                return;
            }

            PendingPropertyChange = ChangePropertyAsync(Properties[value].Id);
        }
    }

    public override Task OnAppearingAsync() => RunSafeAsync(ReloadAsync);

    /// <summary>Re-reads properties, meters and contracts; typed values are kept.</summary>
    public async Task ReloadAsync()
    {
        var properties = await _propertyStore.ListAsync(false);
        Properties = [.. properties.Select(p => new ChoiceOption(p.Id, p.Name, string.Empty))];

        var remembered = Guid.TryParse(_settings.GetString(LastPropertyKey), out var saved) ? saved : (Guid?)null;
        _selectedPropertyId =
            Properties.Any(p => p.Id == _selectedPropertyId) ? _selectedPropertyId
            : Properties.Any(p => p.Id == remembered) ? remembered
            : Properties.FirstOrDefault()?.Id;

        await LoadPropertyAsync();
        IsLoaded = true;
        OnPropertyChanged(nameof(SelectedPropertyIndex));
    }

    /// <summary>
    /// True if the screen may be left: nothing typed, or the user chose Save and it worked, or Don't save (the typed
    /// values are discarded). False if the user cancelled the dialog or the values are invalid.
    /// </summary>
    public async Task<bool> CanLeaveAsync()
    {
        var canLeave = false;
        await RunSafeAsync(async () => canLeave = await CanLeaveCoreAsync());
        return canLeave;
    }

    protected override void RefreshTexts()
    {
        Title = _localizer["SectionReadings"];
        PropertyLabel = _localizer["ReadingsPropertyLabel"];
        DateLabel = _localizer["ReadingsDateLabel"];
        SaveAllText = _localizer["ReadingsSaveAll"];
        NoPropertiesText = _localizer["ReadingsNoProperties"];
        NoMetersText = _localizer["ReadingsNoMeters"];
        HistoryHintText = _localizer["ReadingsHistoryHint"];
        if (IsLoaded)
        {
            Rebuild(KeepEntries());
        }
    }

    void IRecipient<LayoutModeChangedMessage>.Receive(LayoutModeChangedMessage message)
    {
        IsTwoPane = _layoutModeService.Current != LayoutMode.Compact;
        if (!IsTwoPane)
        {
            SelectedHistory = null;
            MarkSelectedCard(null);
        }
    }

    partial void OnReadingDateChanged(DateOnly value)
    {
        foreach (var row in Groups.SelectMany(g => g.Cards).SelectMany(c => c.Rows))
        {
            row.Recompute();
        }
    }

    /// <summary>Switches to another property, asking about typed values first.</summary>
    public Task ChangePropertyAsync(Guid propertyId) => RunSafeAsync(async () =>
    {
        if (propertyId == _selectedPropertyId)
        {
            return;
        }

        if (!await CanLeaveCoreAsync())
        {
            OnPropertyChanged(nameof(SelectedPropertyIndex));
            return;
        }

        _selectedPropertyId = propertyId;
        _settings.SetString(LastPropertyKey, propertyId.ToString());
        SelectedHistory = null;
        _lastSaved = [];
        _dismissedPrompts = [];
        await LoadPropertyAsync();
        OnPropertyChanged(nameof(SelectedPropertyIndex));
    });

    [RelayCommand]
    private Task SaveAllAsync() => RunSafeAsync(async () =>
    {
        if (IsBusy)
        {
            return;
        }

        if (!HasUnsavedValues)
        {
            await _toast.ShowAsync(_localizer["ReadingsNothingToSave"]);
            return;
        }

        await TrySaveAllAsync();
    });

    private async Task<bool> CanLeaveCoreAsync()
    {
        if (!HasUnsavedValues)
        {
            return true;
        }

        switch (await _dialogs.AskUnsavedChangesAsync())
        {
            case UnsavedChangesChoice.Save:
                return await TrySaveAllAsync();
            case UnsavedChangesChoice.DontSave:
                foreach (var row in Groups.SelectMany(g => g.Cards).SelectMany(c => c.Rows))
                {
                    row.Clear();
                }

                OnPropertyChanged(nameof(HasUnsavedValues));
                return true;
            default:
                return false;
        }
    }

    /// <summary>Validates every filled row and stores all of them in one call; false (errors shown in the rows) if any is invalid.</summary>
    private async Task<bool> TrySaveAllAsync()
    {
        var items = new List<(Guid RegisterId, MeterReading Reading)>();
        var saved = new List<(Guid MeterId, MeterReading Reading)>();
        var valid = true;
        foreach (var row in Groups.SelectMany(g => g.Cards).SelectMany(c => c.Rows).Where(r => r.HasInput))
        {
            if (row.TryBuildReading(out var reading))
            {
                items.Add((row.RegisterId, reading));
                saved.Add((row.MeterId, reading));
            }
            else
            {
                valid = false;
            }
        }

        if (!valid || items.Count == 0)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            await _meterStore.AddReadingsAsync(items);
            _lastSaved = saved;
            _dismissedPrompts = [];
            foreach (var row in Groups.SelectMany(g => g.Cards).SelectMany(c => c.Rows))
            {
                row.Clear();
            }

            await LoadPropertyAsync();
        }
        finally
        {
            IsBusy = false;
        }

        await _toast.ShowAsync(_localizer.Get("ReadingsSaved", items.Count));
        return true;
    }

    private async Task LoadPropertyAsync()
    {
        var entries = KeepEntries();
        if (_selectedPropertyId is not { } propertyId)
        {
            _meters = [];
            _contracts = [];
            Rebuild(entries);
            return;
        }

        _meters = await _meterStore.ListByPropertyAsync(propertyId);

        var today = _clock.Today;
        var installed = _meters.Where(m => m.RemovedOn is null).Select(m => m.Id).ToHashSet();
        var suppliers = new Dictionary<Guid, Supplier?>();
        var contracts = new List<ContractInfo>();
        foreach (var contract in await _contractStore.ListByPropertyAsync(propertyId))
        {
            if (contract.IsArchived || contract.ValidFrom > today || contract.ValidTo is { } end && end < today)
            {
                continue;
            }

            var meterIds = (await _contractStore.ListContractMetersAsync(contract.Id))
                .Where(l => MeterContractLink.IsOpen(l, today) && installed.Contains(l.MeterId))
                .Select(l => l.MeterId)
                .ToHashSet();
            if (meterIds.Count == 0)
            {
                continue;
            }

            if (!suppliers.TryGetValue(contract.SupplierId, out var supplier))
            {
                supplier = await _supplierStore.GetAsync(contract.SupplierId);
                suppliers[contract.SupplierId] = supplier;
            }

            var link = supplier is null ? null : contract.GetEffectiveLinks(supplier).FirstOrDefault(l => l.Kind == LinkKind.SubmitReadings);
            contracts.Add(new ContractInfo(contract, supplier, meterIds, link));
        }

        _contracts = contracts;
        Rebuild(entries);
    }

    /// <summary>The typed values and notes by register, to survive a rebuild of the cards.</summary>
    private Dictionary<Guid, (string Value, string Note, bool ShowNote)> KeepEntries() =>
        Groups.SelectMany(g => g.Cards).SelectMany(c => c.Rows)
            .Where(r => r.HasInput || r.NoteText.Length > 0)
            .ToDictionary(r => r.RegisterId, r => (r.ValueText, r.NoteText, r.ShowNote));

    private void Rebuild(Dictionary<Guid, (string Value, string Note, bool ShowNote)> entries)
    {
        var today = _clock.Today;
        Banners = [.. _contracts.Where(c => c.Contract.GetReadingWindow(YearMonth.From(today)) is not null).Select(c => CreateBanner(c, today))];

        var cards = _meters
            .Where(m => m.RemovedOn is null)
            .OrderBy(m => m.Resource)
            .ThenBy(m => m.Location, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(m => m.InstalledOn)
            .Select(m => new MeterCardViewModel(
                m,
                _meters,
                WindowOf(m),
                () => ReadingDate,
                _localizer,
                () => OnPropertyChanged(nameof(HasUnsavedValues)),
                id => _ = RunSafeAsync(() => OpenHistoryAsync(id))))
            .ToList();
        Groups =
        [
            .. cards
                .GroupBy(c => c.Resource)
                .Select(g => new ReadingGroupViewModel(EnumText.Get(_localizer, g.Key), [.. g])),
        ];

        foreach (var row in cards.SelectMany(c => c.Rows))
        {
            if (entries.TryGetValue(row.RegisterId, out var kept))
            {
                row.ShowNote = kept.ShowNote;
                row.NoteText = kept.Note;
                row.ValueText = kept.Value;
            }
        }

        MarkSelectedCard(SelectedHistory?.MeterId);
        Prompts = [.. BuildPrompts()];
        OnPropertyChanged(nameof(HasUnsavedValues));
    }

    /// <summary>The reading window (days) of the first contract that has one and uses the meter.</summary>
    private (int From, int To)? WindowOf(Meter meter) =>
        _contracts
            .Where(c => c.MeterIds.Contains(meter.Id) && c.Contract is { ReadingDayFrom: not null, ReadingDayTo: not null })
            .Select(c => ((int, int)?)(c.Contract.ReadingDayFrom!.Value, c.Contract.ReadingDayTo!.Value))
            .FirstOrDefault();

    private ReadingWindowBannerViewModel CreateBanner(ContractInfo info, DateOnly today)
    {
        var period = YearMonth.From(today);
        var title = info.Contract.Title;
        var submitText = _localizer["ReadingsSubmitOnline"];
        var command = info.SubmitLink is null ? null : new AsyncRelayCommand(() => RunSafeAsync(() => OpenSubmitPageAsync(info)));

        foreach (var candidate in new[] { period.AddMonths(-1), period })
        {
            if (info.Contract.GetReadingWindow(candidate) is { } open && open.From <= today && today <= open.To)
            {
                return new(info.Contract.Id, ReadingWindowState.Open, _localizer.Get("ReadingsWindowOpen", title, FormatRange(open.From, open.To)), submitText, command);
            }
        }

        var current = info.Contract.GetReadingWindow(period)!.Value;
        if (current.From > today)
        {
            var days = current.From.DayNumber - today.DayNumber;
            var text = days == 1
                ? _localizer.Get("ReadingsWindowOpensTomorrow", title, FormatRange(current.From, current.To))
                : _localizer.Get("ReadingsWindowOpens", title, FormatRange(current.From, current.To), days);
            return new(info.Contract.Id, ReadingWindowState.Upcoming, text, submitText, command);
        }

        var next = info.Contract.GetReadingWindow(period.AddMonths(1))!.Value;
        return new(
            info.Contract.Id,
            ReadingWindowState.Closed,
            _localizer.Get("ReadingsWindowClosed", title, next.From.ToString("dd.MM", CultureInfo.InvariantCulture)),
            submitText,
            command);
    }

    private static string FormatRange(DateOnly from, DateOnly to)
    {
        var culture = CultureInfo.CurrentCulture;
        return from.Year == to.Year && from.Month == to.Month
            ? $"{from.Day}–{to.ToString("d MMM", culture)}"
            : $"{from.ToString("d MMM", culture)}–{to.ToString("d MMM", culture)}";
    }

    private async Task OpenSubmitPageAsync(ContractInfo info)
    {
        var uri = info.SubmitLink?.TryGetUri();
        if (uri is null)
        {
            await _dialogs.AlertAsync(
                _localizer["Dialog_Error_Title"],
                ValidationText.For(_localizer, new ValidationError(ErrorCodes.LinkUrlInvalid, nameof(Link.Url))));
            return;
        }

        await _launcher.OpenAsync(uri);
    }

    private IEnumerable<SubmitPromptViewModel> BuildPrompts()
    {
        var savedMeters = _lastSaved.Select(s => s.MeterId).ToHashSet();
        foreach (var info in _contracts)
        {
            var meterIds = info.MeterIds.Where(savedMeters.Contains).ToHashSet();
            var worthAsking = info.SubmitLink is not null || info.Contract.ReadingDayFrom is not null;
            if (meterIds.Count == 0 || !worthAsking || _dismissedPrompts.Contains(info.Contract.Id))
            {
                continue;
            }

            var contractId = info.Contract.Id;
            yield return new SubmitPromptViewModel(
                contractId,
                meterIds,
                _localizer.Get("ReadingsMarkPrompt", info.Supplier?.Name ?? info.Contract.Title),
                _localizer["ReadingsMarkSubmitted"],
                _localizer["ReadingsMarkDismiss"],
                new AsyncRelayCommand(() => RunSafeAsync(() => MarkSubmittedAsync(contractId, meterIds))),
                new RelayCommand(() => Dismiss(contractId)));
        }
    }

    private void Dismiss(Guid contractId)
    {
        _dismissedPrompts.Add(contractId);
        Prompts = [.. Prompts.Where(p => p.ContractId != contractId)];
    }

    /// <summary>Sets <see cref="MeterReading.SubmittedOn"/> to today on the readings just saved for the contract's meters.</summary>
    private async Task MarkSubmittedAsync(Guid contractId, IReadOnlySet<Guid> meterIds)
    {
        var today = _clock.Today;
        var readings = _lastSaved.Where(s => meterIds.Contains(s.MeterId)).Select(s => s.Reading).ToList();
        foreach (var reading in readings)
        {
            reading.SubmittedOn = today;
        }

        await _meterStore.UpdateReadingsAsync(readings);
        Dismiss(contractId);
        await _toast.ShowAsync(_localizer["ReadingsMarked"]);
        if (SelectedHistory is { } history)
        {
            await history.ReloadAsync();
        }
    }

    private async Task OpenHistoryAsync(Guid meterId)
    {
        var history = _historyFactory();
        await history.LoadAsync(meterId);

        if (IsTwoPane)
        {
            history.Changed = () => RunSafeAsync(ReloadAsync);
            SelectedHistory = history;
            MarkSelectedCard(meterId);
        }
        else
        {
            await _navigation.GoToAsync(Routes.ReadingHistory, new Dictionary<string, object> { [EditorNavigation.EditorParameter] = history });
        }
    }

    private void MarkSelectedCard(Guid? meterId)
    {
        foreach (var card in Groups.SelectMany(g => g.Cards))
        {
            card.IsSelected = meterId is { } id && card.MeterId == id;
        }
    }

    /// <summary>A contract that uses installed meters of the property, with its supplier and effective submit link.</summary>
    private sealed record ContractInfo(Contract Contract, Supplier? Supplier, IReadOnlySet<Guid> MeterIds, Link? SubmitLink);
}
