using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>Steps of the replace-meter wizard in order.</summary>
public enum ReplaceMeterStep
{
    Date,
    FinalReading,
    NewMeter,
    Contracts,
    Summary,
}

/// <summary>
/// Wizard that replaces a meter: date, final readings of the old meter, data and initial readings of the new meter,
/// contract links, summary. Nothing is stored until the last step's Replace; Cancel at any step changes nothing.
/// </summary>
public sealed partial class ReplaceMeterWizardViewModel : EditorDialogViewModel
{
    private static readonly int StepCount = Enum.GetValues<ReplaceMeterStep>().Length;

    private readonly ILocalizer _localizer;
    private readonly IClock _clock;
    private readonly IMeterStore _store;
    private readonly Meter _old;
    private readonly DateOnly? _lastReadingDate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsDateStep), nameof(IsFinalStep), nameof(IsNewMeterStep), nameof(IsContractsStep), nameof(IsSummaryStep),
        nameof(IsLastStep), nameof(CanGoBack), nameof(StepTitle), nameof(StepNumberText))]
    private ReplaceMeterStep _step;

    [ObservableProperty]
    private DateOnly _replacementDate;

    [ObservableProperty]
    private string _dateError = string.Empty;

    [ObservableProperty]
    private string _newSerialNumber = string.Empty;

    [ObservableProperty]
    private string _newDigitsText = string.Empty;

    [ObservableProperty]
    private bool _hasVerifiedOn;

    [ObservableProperty]
    private DateOnly _verifiedOn;

    [ObservableProperty]
    private string _newIntervalText = string.Empty;

    [ObservableProperty]
    private string _serialError = string.Empty;

    [ObservableProperty]
    private string _digitsError = string.Empty;

    [ObservableProperty]
    private string _intervalError = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<string> _summaryLines = [];

    [ObservableProperty]
    private ReplaceMeterWizardTexts _texts;

    /// <param name="localizer">Localizer.</param>
    /// <param name="clock">Clock; the replacement date defaults to today.</param>
    /// <param name="store">Store that saves the whole replacement in one transaction.</param>
    /// <param name="oldMeter">The meter to replace, with registers and readings.</param>
    /// <param name="contracts">The contracts of the property that may use the new meter, with the old meter's open links.</param>
    public ReplaceMeterWizardViewModel(
        ILocalizer localizer,
        IClock clock,
        IMeterStore store,
        Meter oldMeter,
        IReadOnlyList<MeterContractLink> contracts)
        : base(localizer)
    {
        ArgumentNullException.ThrowIfNull(oldMeter);
        ArgumentNullException.ThrowIfNull(contracts);

        _localizer = localizer;
        _clock = clock;
        _store = store;
        _old = oldMeter;
        _texts = new ReplaceMeterWizardTexts(localizer);

        var today = clock.Today;
        var registers = oldMeter.Registers.Where(r => !r.IsDeleted).OrderBy(r => r.Kind).ThenBy(r => r.Direction).ToList();
        _lastReadingDate = registers.SelectMany(r => r.Readings).Where(r => !r.IsDeleted).Select(r => (DateOnly?)r.Date).Max();

        var floor = _lastReadingDate is { } last && last > oldMeter.InstalledOn ? last : oldMeter.InstalledOn;
        _replacementDate = today >= floor ? today : floor;
        _verifiedOn = _replacementDate;
        _newDigitsText = oldMeter.Digits?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        _newIntervalText = oldMeter.VerificationIntervalMonths?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;

        Registers = [.. registers.Select(CreateRegisterRow)];
        ContractRows =
        [
            .. contracts
                .OrderBy(c => c.Contract.Title, StringComparer.CurrentCultureIgnoreCase)
                .Select(c => new WizardContractRowViewModel(c, EnumText.Get(localizer, c.Contract.Service))),
        ];
        RefreshTexts();
    }

    public IReadOnlyList<WizardRegisterRowViewModel> Registers { get; }

    public IReadOnlyList<WizardContractRowViewModel> ContractRows { get; }

    public bool HasNoContracts => ContractRows.Count == 0;

    /// <summary>Id of the new meter after a successful <see cref="ReplaceCommand"/>.</summary>
    public Guid? NewMeterId { get; private set; }

    /// <summary>"Cold water · Kitchen · CW-1": what is being replaced.</summary>
    public string OldMeterText =>
        $"{EnumText.Get(_localizer, _old.Resource)} · {MeterNames.Title(_localizer, _old)} · {MeterNames.Serial(_localizer, _old)}";

    /// <summary>"Last reading: 26.09.2026" or empty.</summary>
    public string DateHint => _lastReadingDate is { } date ? _localizer.Get("WizardLastReadingDate", date) : string.Empty;

    public bool IsDateStep => Step == ReplaceMeterStep.Date;

    public bool IsFinalStep => Step == ReplaceMeterStep.FinalReading;

    public bool IsNewMeterStep => Step == ReplaceMeterStep.NewMeter;

    public bool IsContractsStep => Step == ReplaceMeterStep.Contracts;

    public bool IsSummaryStep => Step == ReplaceMeterStep.Summary;

    public bool IsLastStep => Step == ReplaceMeterStep.Summary;

    public bool CanGoBack => Step > ReplaceMeterStep.Date;

    /// <summary>"Step 2 of 5".</summary>
    public string StepNumberText => _localizer.Get("WizardStepOf", (int)Step + 1, StepCount);

    public string StepTitle => Step switch
    {
        ReplaceMeterStep.Date => Texts.DateStepTitle,
        ReplaceMeterStep.FinalReading => Texts.FinalStepTitle,
        ReplaceMeterStep.NewMeter => Texts.NewMeterStepTitle,
        ReplaceMeterStep.Contracts => Texts.ContractsStepTitle,
        _ => Texts.SummaryStepTitle,
    };

    // The base class's Save is not used: the confirm button is ReplaceCommand, which works asynchronously.
    protected override bool TryApply() => false;

    [RelayCommand]
    private void Next()
    {
        if (IsLastStep || !ValidateStep(Step))
        {
            return;
        }

        Step++;
        if (IsSummaryStep)
        {
            SummaryLines = BuildSummary();
        }
    }

    [RelayCommand]
    private void Previous()
    {
        if (CanGoBack)
        {
            Step--;
        }
    }

    /// <summary>Validates every step again, then stores the replacement in one operation and closes the dialog.</summary>
    [RelayCommand]
    private Task ReplaceAsync() => RunSafeAsync(async () =>
    {
        if (IsBusy)
        {
            return;
        }

        foreach (var step in Enum.GetValues<ReplaceMeterStep>().Where(s => s < ReplaceMeterStep.Summary))
        {
            if (!ValidateStep(step))
            {
                Step = step;
                return;
            }
        }

        IsBusy = true;
        try
        {
            // A fresh copy every time: Replace changes the meter it is given, and a failed attempt must leave nothing behind.
            var old = await _store.GetAsync(_old.Id) ?? throw new InvalidOperationException("The meter no longer exists.");
            var values = Registers.ToDictionary(r => r.Kind, r => (Final: ParseValue(r.FinalText), Initial: ParseValue(r.InitialText)));
            var result = MeterReplacement.Replace(old, ReplacementDate, values, _clock);

            var newMeter = result.NewMeter;
            newMeter.SerialNumber = NullIfBlank(NewSerialNumber);
            newMeter.Digits = ParseOptionalInt(NewDigitsText);
            newMeter.VerificationIntervalMonths = ParseOptionalInt(NewIntervalText);
            newMeter.VerifiedOn = HasVerifiedOn ? VerifiedOn : null;
            newMeter.Touch(_clock);

            var close = new List<ContractMeter>();
            var create = new List<ContractMeter>();
            foreach (var row in ContractRows)
            {
                foreach (var link in row.Link.OpenLinks)
                {
                    var end = ReplacementDate.AddDays(-1);
                    close.Add(new ContractMeter
                    {
                        Id = link.Id,
                        CreatedAt = link.CreatedAt,
                        UpdatedAt = link.UpdatedAt,
                        ContractId = link.ContractId,
                        MeterId = link.MeterId,
                        ValidFrom = link.ValidFrom,
                        ValidTo = end < link.ValidFrom ? link.ValidFrom : end,
                    });
                }

                if (row.IsOn)
                {
                    create.Add(new ContractMeter { ContractId = row.Link.Contract.Id, MeterId = newMeter.Id, ValidFrom = ReplacementDate });
                }
            }

            await _store.SaveReplacementAsync(old, newMeter, close, create);
            NewMeterId = newMeter.Id;
        }
        finally
        {
            IsBusy = false;
        }

        CloseSaved();
    });

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        Texts = new ReplaceMeterWizardTexts(_localizer);
        Title = _localizer["WizardTitle"];
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(StepNumberText));
        OnPropertyChanged(nameof(OldMeterText));
        OnPropertyChanged(nameof(DateHint));
        if (IsSummaryStep)
        {
            SummaryLines = BuildSummary();
        }
    }

    private WizardRegisterRowViewModel CreateRegisterRow(MeterRegister register)
    {
        var last = register.Readings.Where(r => !r.IsDeleted).OrderBy(r => r.Date).LastOrDefault();
        var hint = last is null
            ? string.Empty
            : _localizer.Get("WizardLastReading", DecimalInput.Format(last.Value, CultureInfo.CurrentCulture, 3), last.Date);
        var name = $"{EnumText.Get(_localizer, register.Kind)} · {EnumText.Get(_localizer, register.Direction)}";
        return new WizardRegisterRowViewModel(register, name, hint, last?.Value);
    }

    private bool ValidateStep(ReplaceMeterStep step) => step switch
    {
        ReplaceMeterStep.Date => ValidateDate(),
        ReplaceMeterStep.FinalReading => ValidateFinalReadings(),
        ReplaceMeterStep.NewMeter => ValidateNewMeter(),
        _ => true,
    };

    private bool ValidateDate()
    {
        var errors = new List<ValidationError>();
        if (ReplacementDate < _old.InstalledOn)
        {
            errors.Add(new(CommonErrorCodes.WizardDateBeforeInstalled, null, [_old.InstalledOn]));
        }

        if (_lastReadingDate is { } last && ReplacementDate < last)
        {
            errors.Add(new(CommonErrorCodes.WizardDateBeforeLastReading, null, [last]));
        }

        DateError = Join(errors);
        return errors.Count == 0;
    }

    private bool ValidateFinalReadings()
    {
        var valid = true;
        foreach (var row in Registers)
        {
            var errors = new List<ValidationError>();
            row.FinalWarning = string.Empty;
            if (!DecimalInput.TryParse(row.FinalText, CultureInfo.CurrentCulture, out var value))
            {
                errors.Add(new(CommonErrorCodes.NumberInvalid));
            }
            else if (value < 0)
            {
                errors.Add(new(ErrorCodes.MeterReadingValueNegative));
            }
            else if (row.LastValue is { } last && value < last)
            {
                var shown = DecimalInput.Format(last, CultureInfo.CurrentCulture, 3);
                if (_old.Digits is null)
                {
                    errors.Add(new(CommonErrorCodes.WizardFinalBelowLast, null, [shown]));
                }
                else
                {
                    row.FinalWarning = _localizer.Get("WizardRolloverWarning", shown, _old.Digits);
                }
            }

            row.FinalError = Join(errors);
            valid &= errors.Count == 0;
        }

        return valid;
    }

    private bool ValidateNewMeter()
    {
        var errors = new List<ValidationError>();
        SerialError = string.Empty;
        DigitsError = string.Empty;
        IntervalError = string.Empty;

        if (NewSerialNumber.Trim().Length > InputLimits.Code)
        {
            SerialError = Join([new(CommonErrorCodes.TextTooLong, null, [InputLimits.Code])]);
            errors.Add(new(CommonErrorCodes.TextTooLong));
        }

        var digitsErrors = new List<ValidationError>();
        var digits = ParseOptionalInt(NewDigitsText);
        if (digits is null && !string.IsNullOrWhiteSpace(NewDigitsText))
        {
            digitsErrors.Add(new(CommonErrorCodes.NumberInvalid));
        }

        var intervalErrors = new List<ValidationError>();
        var interval = ParseOptionalInt(NewIntervalText);
        if (interval is null && !string.IsNullOrWhiteSpace(NewIntervalText))
        {
            intervalErrors.Add(new(CommonErrorCodes.NumberInvalid));
        }

        // The range rules live in MeterValidator; only the two fields typed in this step are taken from it.
        var candidate = new Meter
        {
            PropertyId = _old.PropertyId,
            Resource = _old.Resource,
            Unit = _old.Unit,
            Digits = digits,
            VerificationIntervalMonths = interval,
            InstalledOn = ReplacementDate,
            Registers = [.. _old.Registers.Where(r => !r.IsDeleted).Select(r => new MeterRegister { Kind = r.Kind, Direction = r.Direction })],
        };
        var rules = MeterValidator.Validate(candidate).Errors;
        digitsErrors.AddRange(rules.Where(e => e.Field == nameof(Meter.Digits)));
        intervalErrors.AddRange(rules.Where(e => e.Field == nameof(Meter.VerificationIntervalMonths)));
        DigitsError = Join(digitsErrors);
        IntervalError = Join(intervalErrors);
        errors.AddRange(digitsErrors);
        errors.AddRange(intervalErrors);

        foreach (var row in Registers)
        {
            row.InitialError = string.Empty;
            if (!DecimalInput.TryParse(row.InitialText, CultureInfo.CurrentCulture, out var value))
            {
                row.InitialError = Join([new(CommonErrorCodes.NumberInvalid)]);
                errors.Add(new(CommonErrorCodes.NumberInvalid));
            }
            else if (value < 0)
            {
                row.InitialError = Join([new(ErrorCodes.MeterReadingValueNegative)]);
                errors.Add(new(ErrorCodes.MeterReadingValueNegative));
            }
        }

        return errors.Count == 0;
    }

    private List<string> BuildSummary()
    {
        var lines = new List<string>
        {
            _localizer.Get("WizardSummaryOld", MeterNames.Serial(_localizer, _old), ReplacementDate),
        };
        foreach (var row in Registers)
        {
            lines.Add(_localizer.Get("WizardSummaryFinal", row.DisplayText, ShowValue(row.FinalText)));
        }

        lines.Add(_localizer.Get(
            "WizardSummaryNew",
            string.IsNullOrWhiteSpace(NewSerialNumber) ? _localizer["MeterNoSerial"] : NewSerialNumber.Trim(),
            ReplacementDate));
        foreach (var row in Registers)
        {
            lines.Add(_localizer.Get("WizardSummaryInitial", row.DisplayText, ShowValue(row.InitialText)));
        }

        var linked = ContractRows.Where(r => r.IsOn).Select(r => r.Title).ToList();
        lines.Add(linked.Count == 0
            ? _localizer["WizardSummaryNoContracts"]
            : _localizer.Get("WizardSummaryContracts", string.Join(", ", linked)));
        return lines;
    }

    private static string ShowValue(string text) => text.Trim();

    private string Join(IEnumerable<ValidationError> errors) =>
        string.Join("\n", errors.Select(e => ValidationText.For(_localizer, e)));

    private decimal ParseValue(string text) =>
        DecimalInput.TryParse(text, CultureInfo.CurrentCulture, out var value)
            ? value
            : throw new InvalidOperationException("A reading was not validated.");

    private static int? ParseOptionalInt(string text) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static string? NullIfBlank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
