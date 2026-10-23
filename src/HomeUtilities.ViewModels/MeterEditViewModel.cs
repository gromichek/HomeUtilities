using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>Editor of one meter of a property. It is a page in every layout; see <see cref="Routes.MeterEdit"/>.</summary>
public sealed partial class MeterEditViewModel : PaneEditorViewModel
{
    private static readonly Regex RegisterErrorField = new(@"^Registers\[(\d+)\]", RegexOptions.Compiled);

    /// <summary>The properties that mark the editor dirty when the user changes them.</summary>
    private static readonly HashSet<string> DirtyFields =
    [
        nameof(SelectedResource), nameof(SelectedUnit), nameof(SerialNumber), nameof(Location), nameof(DigitsText), nameof(Notes),
        nameof(InstalledOn), nameof(HasVerifiedOn), nameof(VerifiedOn), nameof(IntervalText), nameof(HasDueOverride), nameof(DueOverride),
    ];

    /// <summary>The properties the "next verification" line depends on.</summary>
    private static readonly HashSet<string> VerificationFields =
    [
        nameof(InstalledOn), nameof(HasVerifiedOn), nameof(VerifiedOn), nameof(IntervalText), nameof(HasDueOverride), nameof(DueOverride),
    ];

    private readonly IMeterStore _store;
    private readonly IContractStore _contractStore;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;
    private readonly INavigationService _navigation;
    private readonly IToastService _toast;
    private readonly Func<MeterEditViewModel> _editorFactory;
    private readonly List<RegisterRowViewModel> _registers = [];

    private Meter _meter = new();
    private Meter? _pending;
    private Guid _propertyId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedResourceIndex))]
    private IReadOnlyList<EnumOption> _resources = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedResourceIndex))]
    private EnumOption? _selectedResource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedUnitIndex))]
    private IReadOnlyList<EnumOption> _units = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedUnitIndex))]
    private EnumOption? _selectedUnit;

    [ObservableProperty]
    private string _serialNumber = string.Empty;

    [ObservableProperty]
    private string _location = string.Empty;

    [ObservableProperty]
    private string _digitsText = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private DateOnly _installedOn;

    [ObservableProperty]
    private bool _hasVerifiedOn;

    [ObservableProperty]
    private DateOnly _verifiedOn;

    [ObservableProperty]
    private string _intervalText = string.Empty;

    [ObservableProperty]
    private bool _hasDueOverride;

    [ObservableProperty]
    private DateOnly _dueOverride;

    [ObservableProperty]
    private IReadOnlyList<RegisterRowViewModel> _registerRows = [];

    [ObservableProperty]
    private IReadOnlyList<MeterContractSwitchViewModel> _contractSwitches = [];

    [ObservableProperty]
    private IReadOnlyList<MeterHistoryRowViewModel> _historyRows = [];

    [ObservableProperty]
    private MeterEditTexts _texts;

    public MeterEditViewModel(
        IMeterStore store,
        IContractStore contractStore,
        IClock clock,
        ILocalizer localizer,
        IDialogService dialogs,
        INavigationService navigation,
        IToastService toast,
        Func<MeterEditViewModel> editorFactory)
        : base(localizer, dialogs, navigation, toast)
    {
        _store = store;
        _contractStore = contractStore;
        _clock = clock;
        _localizer = localizer;
        _navigation = navigation;
        _toast = toast;
        _editorFactory = editorFactory;
        _texts = new MeterEditTexts(localizer);
        InstalledOn = clock.Today;
        VerifiedOn = clock.Today;
        DueOverride = clock.Today;
        RefreshTexts();
    }

    /// <summary>Property a new meter belongs to; set before <see cref="LoadAsync(Guid?)"/>.</summary>
    public Guid PropertyId
    {
        get => _propertyId;
        set => _propertyId = value;
    }

    public override Guid EntityId => _meter.Id;

    /// <summary>Meters that have readings are never deleted (they are replaced instead).</summary>
    public override bool CanShowDelete => !IsNew && !HasReadings;

    /// <summary>Hint shown instead of the Delete button for a meter with readings.</summary>
    public bool ShowDeleteBlocked => !IsNew && HasReadings;

    /// <summary>True if any register of the stored meter has a reading.</summary>
    public bool HasReadings => _registers.Any(r => r.HasReadings);

    /// <summary>The "Replace meter…" action: only for a stored meter that is still installed.</summary>
    public bool CanReplace => !IsNew && _meter.RemovedOn is null;

    public int SelectedResourceIndex
    {
        get => SelectedResource is null ? -1 : Resources.ToList().IndexOf(SelectedResource);
        set
        {
            if (value >= 0 && value < Resources.Count)
            {
                SelectedResource = Resources[value];
            }
        }
    }

    public int SelectedUnitIndex
    {
        get => SelectedUnit is null ? -1 : Units.ToList().IndexOf(SelectedUnit);
        set
        {
            if (value >= 0 && value < Units.Count)
            {
                SelectedUnit = Units[value];
            }
        }
    }

    public string UnitError => Lookup(nameof(Meter.Unit));

    public string SerialNumberError => Lookup(nameof(Meter.SerialNumber));

    public string LocationError => Lookup(nameof(Meter.Location));

    public string DigitsError => Lookup(nameof(Meter.Digits));

    public string IntervalError => Lookup(nameof(Meter.VerificationIntervalMonths));

    public string InstalledOnError => Lookup(nameof(Meter.InstalledOn));

    public string NotesError => Lookup(nameof(Meter.Notes));

    /// <summary>Errors about the registers as a whole (no register, duplicate, export mismatch).</summary>
    public string RegistersError => string.Join(
        "\n",
        Errors.Where(e => e.Key.StartsWith("Registers", StringComparison.Ordinal)).Select(e => e.Value));

    /// <summary>"Next verification: 01.10.2026", or empty when no date can be calculated.</summary>
    public string VerificationLine
    {
        get
        {
            var meter = BuildMeter(null);
            return meter.GetVerificationDue() is { } due ? _localizer.Get("MeterNextVerification", due) : string.Empty;
        }
    }

    public bool HasVerificationLine => VerificationLine.Length > 0;

    /// <summary>Overdue / due soon of the meter as currently entered.</summary>
    public VerificationStatus VerificationStatus => MeterVerification.GetStatus(BuildMeter(null), _clock.Today);

    /// <summary>"Verification overdue since …" / "Verification due …"; empty when there is nothing to warn about.</summary>
    public string VerificationBadgeText
    {
        get
        {
            var meter = BuildMeter(null);
            return MeterNames.BadgeText(_localizer, meter, MeterVerification.GetStatus(meter, _clock.Today));
        }
    }

    public bool IsVerificationOverdue => VerificationStatus == VerificationStatus.Overdue;

    public bool IsVerificationDueSoon => VerificationStatus == VerificationStatus.DueSoon;

    /// <summary>The dual-tariff quick action: only while the single Total register has no readings.</summary>
    public bool CanApplyDualTariff =>
        _registers.Any(r => r.Kind == RegisterKind.Total && !r.HasReadings)
        && !_registers.Any(r => r.Kind is RegisterKind.Day or RegisterKind.Night);

    public bool HasNoContracts => ContractSwitches.Count == 0;

    public bool HasHistory => HistoryRows.Count > 0;

    public Task LoadAsync(Guid propertyId, Guid? id)
    {
        PropertyId = propertyId;
        return LoadAsync(id);
    }

    public override async Task LoadAsync(Guid? id)
    {
        var previous = SuppressDirty;
        SuppressDirty = true;
        try
        {
            var existing = id is { } value ? await _store.GetAsync(value) : null;
            _meter = existing ?? NewMeter();
            _propertyId = _meter.PropertyId;
            IsNew = existing is null;

            Resources = EnumText.Options<MeterResource>(_localizer);
            SelectedResource = Resources.First(o => o.Value.Equals(_meter.Resource));
            RebuildUnits(_meter.Unit);
            SerialNumber = _meter.SerialNumber ?? string.Empty;
            Location = _meter.Location ?? string.Empty;
            DigitsText = _meter.Digits?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            Notes = _meter.Notes ?? string.Empty;
            InstalledOn = _meter.InstalledOn;
            HasVerifiedOn = _meter.VerifiedOn is not null;
            VerifiedOn = _meter.VerifiedOn ?? _clock.Today;
            IntervalText = _meter.VerificationIntervalMonths?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            HasDueOverride = _meter.NextVerificationDue is not null;
            DueOverride = _meter.NextVerificationDue ?? _clock.Today;

            _registers.Clear();
            foreach (var register in _meter.Registers.Where(r => !r.IsDeleted))
            {
                _registers.Add(CreateRegisterRow(register));
            }

            await LoadContractsAsync();
            await LoadHistoryAsync();
            ClearErrors();
            RebuildRegisters();
            Title = TitleText();
            IsDirty = false;
        }
        finally
        {
            SuppressDirty = previous;
        }

        OnPropertyChanged(nameof(CanReplace));
        NotifyVerificationChanged();
    }

    private Meter NewMeter()
    {
        var meter = new Meter
        {
            PropertyId = _propertyId,
            Resource = MeterResource.ColdWater,
            Unit = MeterUnit.M3,
            InstalledOn = _clock.Today,
        };
        meter.Registers.Add(MeterRegister.CreateDefault(meter.Id));
        return meter;
    }

    private async Task LoadContractsAsync()
    {
        var today = _clock.Today;
        var rows = new List<MeterContractSwitchViewModel>();
        if (_propertyId != Guid.Empty)
        {
            foreach (var contract in await _contractStore.ListByPropertyAsync(_propertyId))
            {
                var open = IsNew
                    ? []
                    : (await _contractStore.ListContractMetersAsync(contract.Id))
                        .Where(l => l.MeterId == _meter.Id && MeterContractLink.IsOpen(l, today))
                        .ToList();
                var inUse = open.Count > 0;
                if (!inUse && (contract.IsArchived || contract.ValidTo is { } end && end < today))
                {
                    continue;
                }

                var row = new MeterContractSwitchViewModel(
                    new MeterContractLink(contract, open), EnumText.Get(_localizer, contract.Service), inUse);
                row.Toggled = MarkDirty;
                rows.Add(row);
            }
        }

        ContractSwitches = [.. rows.OrderBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase)];
        OnPropertyChanged(nameof(HasNoContracts));
    }

    private async Task LoadHistoryAsync()
    {
        var rows = new List<MeterHistoryRowViewModel>();
        if (!IsNew)
        {
            var all = await _store.ListByPropertyAsync(_propertyId);
            if (all.FirstOrDefault(m => m.Id == _meter.ReplacedMeterId) is { } previous)
            {
                rows.Add(new MeterHistoryRowViewModel(
                    previous.Id,
                    _localizer.Get("MeterHistoryReplaced", MeterNames.Serial(_localizer, previous), _meter.InstalledOn),
                    id => OpenMeterCommand.Execute(id)));
            }

            if (all.FirstOrDefault(m => m.ReplacedMeterId == _meter.Id) is { } next)
            {
                rows.Add(new MeterHistoryRowViewModel(
                    next.Id,
                    _localizer.Get("MeterHistoryReplacedBy", MeterNames.Serial(_localizer, next), next.InstalledOn),
                    id => OpenMeterCommand.Execute(id)));
            }
        }

        HistoryRows = rows;
        OnPropertyChanged(nameof(HasHistory));
    }

    [RelayCommand]
    private Task OpenMeterAsync(Guid id) => RunSafeAsync(async () =>
    {
        var editor = _editorFactory();
        editor.IsHostedInPage = true;
        editor.Finished = Finished;
        await editor.LoadAsync(_propertyId, id);
        await _navigation.GoToAsync(Routes.MeterEdit, new Dictionary<string, object> { [EditorNavigation.EditorParameter] = editor });
    });

    [RelayCommand]
    private Task AddRegisterAsync() => RunSafeAsync(async () =>
    {
        var used = _registers.Select(r => r.Kind).ToHashSet();
        var editor = new RegisterEditorViewModel(_localizer, used);
        if (editor.Kinds.Count == 0)
        {
            await Dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer["MeterRegisterNoneLeft"]);
            return;
        }

        if (!await Dialogs.ShowEditorAsync(editor) || editor.Result is not { } result)
        {
            return;
        }

        _registers.Add(CreateRegisterRow(new MeterRegister { MeterId = _meter.Id, Kind = result.Kind, Direction = result.Direction }));
        RebuildRegisters();
        MarkDirty();
    });

    [RelayCommand]
    private Task RemoveRegisterAsync(RegisterRowViewModel row) => RunSafeAsync(async () =>
    {
        if (row.HasReadings)
        {
            await Dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer["MeterRegisterHasReadings"]);
            return;
        }

        var confirmed = await Dialogs.ConfirmAsync(
            _localizer["Dialog_DeleteRow_Title"],
            _localizer.Get("Dialog_DeleteRow_Message", row.DisplayText),
            _localizer["Delete"],
            _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        _registers.Remove(row);
        RebuildRegisters();
        MarkDirty();
    });

    private bool CanDualTariff() => CanApplyDualTariff;

    /// <summary>Replaces the unused Total register by Day and Night registers.</summary>
    [RelayCommand(CanExecute = nameof(CanDualTariff))]
    private void ApplyDualTariff()
    {
        var total = _registers.FirstOrDefault(r => r.Kind == RegisterKind.Total && !r.HasReadings);
        if (total is null || !CanApplyDualTariff)
        {
            return;
        }

        _registers.Remove(total);
        _registers.Add(CreateRegisterRow(new MeterRegister { MeterId = _meter.Id, Kind = RegisterKind.Day, Direction = EnergyDirection.Import }));
        _registers.Add(CreateRegisterRow(new MeterRegister { MeterId = _meter.Id, Kind = RegisterKind.Night, Direction = EnergyDirection.Import }));
        RebuildRegisters();
        MarkDirty();
    }

    /// <summary>Opens the replacement wizard for the installed meter; unsaved changes are saved first (or discarded on request).</summary>
    [RelayCommand]
    private Task ReplaceAsync() => RunSafeAsync(async () =>
    {
        if (!CanReplace || !await CanLeaveAsync())
        {
            return;
        }

        var fresh = await _store.GetAsync(_meter.Id);
        if (fresh is null || fresh.RemovedOn is not null)
        {
            return;
        }

        var today = _clock.Today;
        var contracts = new List<MeterContractLink>();
        foreach (var contract in await _contractStore.ListByPropertyAsync(_propertyId))
        {
            var open = (await _contractStore.ListContractMetersAsync(contract.Id))
                .Where(l => l.MeterId == fresh.Id && MeterContractLink.IsOpen(l, today))
                .ToList();
            if (open.Count > 0 || (!contract.IsArchived && (contract.ValidTo is not { } end || end >= today)))
            {
                contracts.Add(new MeterContractLink(contract, open));
            }
        }

        var wizard = new ReplaceMeterWizardViewModel(_localizer, _clock, _store, fresh, contracts);
        if (!await Dialogs.ShowEditorAsync(wizard))
        {
            return;
        }

        IsDirty = false;
        await _toast.ShowAsync(_localizer["MeterReplaced"]);
        await _navigation.GoBackAsync();
        if (Finished is { } finished)
        {
            await finished(EditOutcome.Saved);
        }
    });

    protected override IReadOnlyList<ValidationError> Validate()
    {
        var inputErrors = new List<ValidationError>();
        _pending = BuildMeter(inputErrors);

        var errors = new List<ValidationError>(inputErrors);
        errors.AddRange(MeterValidator.Validate(_pending).Errors);
        errors.AddRange(TextLengthErrors());
        if (ReadingsBeforeInstallation(_pending))
        {
            errors.Add(new(ErrorCodes.MeterReadingDateOutsideInstallation, nameof(Meter.InstalledOn)));
        }

        return errors;
    }

    protected override async Task SaveCoreAsync()
    {
        var pending = _pending ?? BuildMeter(null);
        await _store.SaveAsync(pending);
        await SaveContractLinksAsync(pending);
        await LoadAsync(pending.Id);
    }

    protected override Task DeleteCoreAsync()
    {
        if (HasReadings)
        {
            throw new InvalidOperationException("A meter with readings cannot be deleted.");
        }

        return _store.DeleteAsync(_meter.Id);
    }

    protected override string DeleteName() => MeterNames.Title(_localizer, _meter);

    private async Task SaveContractLinksAsync(Meter saved)
    {
        var today = _clock.Today;
        foreach (var row in ContractSwitches)
        {
            if (row.IsOn && !row.Link.IsLinked)
            {
                var link = new ContractMeter
                {
                    ContractId = row.ContractId,
                    MeterId = saved.Id,
                    ValidFrom = saved.InstalledOn > today ? saved.InstalledOn : today,
                };
                await _contractStore.SaveContractMeterAsync(link);
            }
            else if (!row.IsOn && row.Link.IsLinked)
            {
                foreach (var link in row.Link.OpenLinks)
                {
                    var yesterday = today.AddDays(-1);
                    link.ValidTo = yesterday < link.ValidFrom ? link.ValidFrom : yesterday;
                    await _contractStore.SaveContractMeterAsync(link);
                }
            }
        }
    }

    protected override void OnErrorsChanged()
    {
        OnPropertyChanged(nameof(UnitError));
        OnPropertyChanged(nameof(SerialNumberError));
        OnPropertyChanged(nameof(LocationError));
        OnPropertyChanged(nameof(DigitsError));
        OnPropertyChanged(nameof(IntervalError));
        OnPropertyChanged(nameof(InstalledOnError));
        OnPropertyChanged(nameof(NotesError));
        OnPropertyChanged(nameof(RegistersError));
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is not { } name)
        {
            return;
        }

        if (DirtyFields.Contains(name))
        {
            MarkDirty();
        }

        if (VerificationFields.Contains(name))
        {
            NotifyVerificationChanged();
        }

        if (name == nameof(IsNew))
        {
            OnPropertyChanged(nameof(CanReplace));
            OnPropertyChanged(nameof(ShowDeleteBlocked));
        }
    }

    partial void OnSelectedResourceChanged(EnumOption? value)
    {
        if (value is null || SuppressDirty)
        {
            return;
        }

        var resource = value.As<MeterResource>();
        var current = SelectedUnit?.As<MeterUnit>();
        RebuildUnits(current is { } unit && MeterUnitRules.IsCompatible(resource, unit) ? unit : MeterUnitRules.DefaultUnit(resource));
    }

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        var previous = SuppressDirty;
        SuppressDirty = true;
        try
        {
            Texts = new MeterEditTexts(_localizer);
            var resource = SelectedResource?.As<MeterResource>() ?? MeterResource.ColdWater;
            Resources = EnumText.Options<MeterResource>(_localizer);
            SelectedResource = Resources.First(o => o.Value.Equals(resource));
            RebuildUnits(SelectedUnit?.As<MeterUnit>());
            DeleteWholeText = Texts.DeleteMeterText;
            Title = TitleText();
            RebuildRegisters();
            OnPropertyChanged(nameof(VerificationLine));
            OnPropertyChanged(nameof(VerificationBadgeText));
        }
        finally
        {
            SuppressDirty = previous;
        }
    }

    private void RebuildUnits(MeterUnit? preferred)
    {
        var resource = SelectedResource?.As<MeterResource>() ?? MeterResource.ColdWater;
        var allowed = MeterUnitRules.UnitsFor(resource);
        var options = EnumText.Options<MeterUnit>(_localizer);
        Units = [.. allowed.Select(unit => options.First(o => o.Value.Equals(unit)))];
        var unit = preferred is { } value && allowed.Contains(value) ? value : MeterUnitRules.DefaultUnit(resource);
        SelectedUnit = Units.First(o => o.Value.Equals(unit));
    }

    private RegisterRowViewModel CreateRegisterRow(MeterRegister register)
    {
        var row = new RegisterRowViewModel(register, _localizer);
        row.RemoveCommand = new RelayCommand(() => RemoveRegisterCommand.Execute(row));
        return row;
    }

    private void RebuildRegisters()
    {
        // Rows were created with the texts of the language at that time; recreate them so the names follow a language switch.
        var rebuilt = _registers.Select(r => CreateRegisterRow(r.Original)).ToList();
        _registers.Clear();
        _registers.AddRange(rebuilt);
        RegisterRows = [.. _registers.OrderBy(r => r.Kind).ThenBy(r => r.Direction)];
        OnPropertyChanged(nameof(HasReadings));
        OnPropertyChanged(nameof(CanShowDelete));
        OnPropertyChanged(nameof(ShowFormDelete));
        OnPropertyChanged(nameof(ShowDeleteBlocked));
        OnPropertyChanged(nameof(CanApplyDualTariff));
        ApplyDualTariffCommand.NotifyCanExecuteChanged();
    }

    private void NotifyVerificationChanged()
    {
        OnPropertyChanged(nameof(VerificationLine));
        OnPropertyChanged(nameof(HasVerificationLine));
        OnPropertyChanged(nameof(VerificationStatus));
        OnPropertyChanged(nameof(VerificationBadgeText));
        OnPropertyChanged(nameof(IsVerificationOverdue));
        OnPropertyChanged(nameof(IsVerificationDueSoon));
    }

    private string TitleText() => IsNew ? _localizer["MeterNew"] : MeterNames.Title(_localizer, _meter);

    private string Lookup(string key) => Errors.TryGetValue(key, out var message) ? message : string.Empty;

    private IEnumerable<ValidationError> TextLengthErrors()
    {
        if (SerialNumber.Trim().Length > InputLimits.Code)
        {
            yield return new(CommonErrorCodes.TextTooLong, nameof(Meter.SerialNumber), [InputLimits.Code]);
        }

        if (Location.Trim().Length > InputLimits.Name)
        {
            yield return new(CommonErrorCodes.TextTooLong, nameof(Meter.Location), [InputLimits.Name]);
        }
    }

    private static bool ReadingsBeforeInstallation(Meter meter) =>
        meter.Registers.Any(register => register.Readings.Any(
            reading => !reading.IsDeleted && MeterReadingValidator.Validate(reading, register, meter).Errors
                .Any(e => e.Code == ErrorCodes.MeterReadingDateOutsideInstallation)));

    private int? ParseWholeNumber(string text, string field, List<ValidationError>? errors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        errors?.Add(new ValidationError(CommonErrorCodes.NumberInvalid, field));
        return null;
    }

    /// <summary>Builds the meter from the entered values; unparsable numbers are reported in <paramref name="inputErrors"/> and left empty.</summary>
    private Meter BuildMeter(List<ValidationError>? inputErrors)
    {
        var meter = new Meter
        {
            Id = _meter.Id,
            CreatedAt = _meter.CreatedAt,
            UpdatedAt = _meter.UpdatedAt,
            PropertyId = _propertyId,
            Resource = SelectedResource?.As<MeterResource>() ?? MeterResource.ColdWater,
            Unit = SelectedUnit?.As<MeterUnit>() ?? MeterUnit.M3,
            SerialNumber = NullIfBlank(SerialNumber),
            Location = NullIfBlank(Location),
            Digits = ParseWholeNumber(DigitsText, nameof(Meter.Digits), inputErrors),
            InstalledOn = InstalledOn,
            RemovedOn = _meter.RemovedOn,
            ReplacedMeterId = _meter.ReplacedMeterId,
            VerifiedOn = HasVerifiedOn ? VerifiedOn : null,
            VerificationIntervalMonths = ParseWholeNumber(IntervalText, nameof(Meter.VerificationIntervalMonths), inputErrors),
            NextVerificationDue = HasDueOverride ? DueOverride : null,
            Notes = NullIfBlank(Notes),
        };

        foreach (var row in _registers)
        {
            // The stored readings travel with the register: a save merges children, so leaving them out would delete them.
            meter.Registers.Add(new MeterRegister
            {
                Id = row.Id,
                CreatedAt = row.CreatedAt,
                MeterId = meter.Id,
                Kind = row.Kind,
                Direction = row.Direction,
                Name = row.Name,
                Readings = row.Original.Readings,
            });
        }

        return meter;
    }

    private static string? NullIfBlank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
