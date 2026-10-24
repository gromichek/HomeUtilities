using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The entry row of one register on the readings screen: last reading, the new value being typed, the live
/// consumption preview and the warnings or errors that go with it.
/// </summary>
public sealed partial class RegisterEntryViewModel : ObservableObject
{
    /// <summary>How many earlier months the anomaly check looks at.</summary>
    private const int HistoryMonths = 12;

    private readonly Meter _meter;
    private readonly MeterRegister _register;
    private readonly MeterChain _chain;
    private readonly (int From, int To)? _window;
    private readonly Func<DateOnly> _readingDate;
    private readonly ILocalizer _localizer;
    private readonly Action _changed;
    private readonly MeterReading? _last;

    [ObservableProperty]
    private string _valueText = string.Empty;

    [ObservableProperty]
    private string _noteText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoteToggleText))]
    private bool _showNote;

    [ObservableProperty]
    private string _previewText = string.Empty;

    [ObservableProperty]
    private string _warningText = string.Empty;

    [ObservableProperty]
    private string _errorText = string.Empty;

    public RegisterEntryViewModel(
        Meter meter,
        MeterRegister register,
        MeterChain chain,
        (int From, int To)? window,
        Func<DateOnly> readingDate,
        ILocalizer localizer,
        Action changed)
    {
        _meter = meter;
        _register = register;
        _chain = chain;
        _window = window;
        _readingDate = readingDate;
        _localizer = localizer;
        _changed = changed;

        Label = RegisterLabel.For(localizer, meter, register);
        _last = ChainReadings().OrderBy(r => r.Date).LastOrDefault();
        LastText = _last is { } last
            ? localizer.Get("ReadingsLast", Format(last.Value), last.Date.ToString("d", CultureInfo.CurrentCulture))
            : localizer["ReadingsNoLast"];
        UnitText = EnumText.Get(localizer, meter.Unit);
        ToggleNoteCommand = new RelayCommand(() => ShowNote = !ShowNote);
    }

    public Guid RegisterId => _register.Id;

    public Guid MeterId => _meter.Id;

    /// <summary>"Day" / "Night" / "Export"; empty for a single Total register.</summary>
    public string Label { get; }

    public bool HasLabel => Label.Length > 0;

    /// <summary>"last: 437,097 · 26.09.2026".</summary>
    public string LastText { get; }

    public string UnitText { get; }

    public string NoteToggleText => _localizer[ShowNote ? "ReadingsNoteHide" : "ReadingsNoteShow"];

    public IRelayCommand ToggleNoteCommand { get; }

    /// <summary>The consumption since the last reading as a number; null while nothing valid is typed or there is no earlier reading.</summary>
    public decimal? Consumption { get; private set; }

    /// <summary>Codes of the warnings currently shown (<see cref="ConsumptionWarningCodes"/>).</summary>
    public IReadOnlyList<string> WarningCodes { get; private set; } = [];

    /// <summary>True when a new value was typed (a note alone does not count).</summary>
    public bool HasInput => !string.IsNullOrWhiteSpace(ValueText);

    public bool HasPreview => PreviewText.Length > 0;

    public bool HasWarning => WarningText.Length > 0;

    public bool HasError => ErrorText.Length > 0;

    /// <summary>Empties the value and the note.</summary>
    public void Clear()
    {
        ValueText = string.Empty;
        NoteText = string.Empty;
        ShowNote = false;
    }

    /// <summary>Recalculates the preview, warnings and errors; called when the text or the reading date changed.</summary>
    public void Recompute()
    {
        Consumption = null;
        WarningCodes = [];
        PreviewText = string.Empty;
        WarningText = string.Empty;
        ErrorText = string.Empty;
        NotifyMessages();

        if (!HasInput)
        {
            return;
        }

        if (!DecimalInput.TryParse(ValueText, CultureInfo.CurrentCulture, out var value))
        {
            ErrorText = ValidationText.For(_localizer, new ValidationError(CommonErrorCodes.NumberInvalid, nameof(MeterReading.Value)));
            NotifyMessages();
            return;
        }

        var reading = NewReading(value);
        var errors = MeterReadingValidator.Validate(reading, _register, _meter).Errors;
        if (errors.Count > 0)
        {
            ErrorText = string.Join("\n", errors.Select(e => ValidationText.For(_localizer, e)));
            NotifyMessages();
            return;
        }

        Preview(reading);
        NotifyMessages();
    }

    /// <summary>
    /// Builds the reading for saving. False when there is nothing typed or the typed value is invalid
    /// (the error is then shown in <see cref="ErrorText"/>).
    /// </summary>
    public bool TryBuildReading(out MeterReading reading)
    {
        Recompute();
        reading = null!;
        if (!HasInput || HasError || !DecimalInput.TryParse(ValueText, CultureInfo.CurrentCulture, out var value))
        {
            return false;
        }

        reading = NewReading(value);
        return true;
    }

    partial void OnValueTextChanged(string value)
    {
        Recompute();
        _changed();
    }

    partial void OnNoteTextChanged(string value) => _changed();

    private MeterReading NewReading(decimal value) => new()
    {
        RegisterId = _register.Id,
        Date = _readingDate(),
        Value = value,
        Kind = ReadingKind.Regular,
        Note = string.IsNullOrWhiteSpace(NoteText) ? null : NoteText.Trim(),
    };

    private void Preview(MeterReading reading)
    {
        var warnings = new List<string>();
        var codes = new List<string>();
        var last = _last;
        if (last is not null && reading.Date < last.Date)
        {
            warnings.Add(_localizer.Get("ReadingsWarnDateBefore", last.Date.ToString("d", CultureInfo.CurrentCulture)));
        }

        // The previous reading is the latest one before the new date, in any meter of the chain.
        var previous = ChainReadings().Where(r => r.Date < reading.Date).OrderBy(r => r.Date).LastOrDefault();
        if (previous is null)
        {
            WarningText = string.Join("\n", warnings);
            return;
        }

        // The calculator works on stored readings, so the new one is added for the calculation only.
        _register.Readings.Add(reading);
        try
        {
            var result = ConsumptionCalculator.Calculate(_chain, _register.Kind, previous.Date, reading.Date);
            if (!result.IsComplete)
            {
                if (result.Warnings.Any(w => w.Code == ConsumptionWarningCodes.NegativeConsumption))
                {
                    ErrorText = _localizer.Get("ReadingsErrLower", Format(previous.Value));
                }

                return;
            }

            Consumption = result.Quantity;
            PreviewText = $"+ {Format(result.Quantity)} {UnitText}";
            if (result.Warnings.Any(w => w.Code == ConsumptionWarningCodes.RolloverAssumed))
            {
                codes.Add(ConsumptionWarningCodes.RolloverAssumed);
                var max = Pow10(_meter.Digits ?? 0) - 1;
                warnings.Add(_localizer.Get("ReadingsWarnRollover", Format(max), Format(reading.Value)));
            }

            var history = ConsumptionHistory.Monthly(_chain, _register.Kind, YearMonth.From(reading.Date).AddMonths(-1), HistoryMonths, _window);
            foreach (var warning in ConsumptionAnomalies.Check(result.Quantity, history))
            {
                codes.Add(warning.Code);
                warnings.Add(warning.Code switch
                {
                    ConsumptionWarningCodes.UnusuallyHigh => _localizer.Get("ReadingsWarnHigh", Format((decimal)warning.Args![1]!)),
                    ConsumptionWarningCodes.ZeroConsumption => _localizer["ReadingsWarnZero"],
                    _ => string.Empty,
                });
            }
        }
        finally
        {
            _register.Readings.Remove(reading);
            WarningCodes = codes;
            WarningText = string.Join("\n", warnings.Where(w => w.Length > 0));
        }
    }

    /// <summary>Non-deleted readings of the registers of this kind over all meters of the chain.</summary>
    private IEnumerable<MeterReading> ChainReadings() =>
        _chain.Meters
            .SelectMany(m => m.Registers.Where(r => !r.IsDeleted && r.Kind == _register.Kind))
            .SelectMany(r => r.Readings.Where(x => !x.IsDeleted));

    private void NotifyMessages()
    {
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(HasWarning));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(Consumption));
        OnPropertyChanged(nameof(WarningCodes));
    }

    private static string Format(decimal value) => DecimalInput.Format(value, CultureInfo.CurrentCulture, 3);

    private static decimal Pow10(int digits)
    {
        var result = 1m;
        for (var i = 0; i < digits; i++)
        {
            result *= 10m;
        }

        return result;
    }
}
