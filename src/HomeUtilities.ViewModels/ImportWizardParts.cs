using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Import;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>An entry of the period rule picker.</summary>
public sealed record ImportPeriodRuleChoice(ImportPeriodRule Value, string Text);

/// <summary>One "from this date on use that rule" line of the mapping step.</summary>
public sealed partial class ImportRuleChangeRowViewModel : ObservableObject
{
    private readonly Action<ImportRuleChangeRowViewModel> _remove;

    /// <summary>Creates a row.</summary>
    /// <param name="change">The rule change to show.</param>
    /// <param name="rules">The rules to choose from.</param>
    /// <param name="fromLabel">Label of the date.</param>
    /// <param name="dayLimitLabel">Label of the day limit.</param>
    /// <param name="removeText">Text of the remove button.</param>
    /// <param name="remove">Called when the owner removes the row.</param>
    public ImportRuleChangeRowViewModel(
        PeriodRuleChange change,
        IReadOnlyList<ImportPeriodRuleChoice> rules,
        string fromLabel,
        string dayLimitLabel,
        string removeText,
        Action<ImportRuleChangeRowViewModel> remove)
    {
        _remove = remove;
        Rules = rules;
        FromLabel = fromLabel;
        DayLimitLabel = dayLimitLabel;
        RemoveText = removeText;
        _fromDate = change.FromDate.ToDateTime(TimeOnly.MinValue);
        _selectedRule = rules.FirstOrDefault(r => r.Value == change.Rule) ?? rules[0];
        _dayLimitText = change.DayLimit.ToString(CultureInfo.InvariantCulture);
    }

    [ObservableProperty]
    private DateTime _fromDate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDayLimit))]
    private ImportPeriodRuleChoice _selectedRule;

    [ObservableProperty]
    private string _dayLimitText;

    /// <summary>The rules to choose from.</summary>
    public IReadOnlyList<ImportPeriodRuleChoice> Rules { get; }

    public string FromLabel { get; }

    public string DayLimitLabel { get; }

    public string RemoveText { get; }

    /// <summary>True when the rule uses the day limit.</summary>
    public bool ShowDayLimit => SelectedRule.Value == ImportPeriodRule.PreviousMonthIfDayUpTo;

    /// <summary>The rule change as a profile entry; an unreadable day limit becomes 0 (the profile check reports it).</summary>
    public PeriodRuleChange ToChange() => new()
    {
        FromDate = DateOnly.FromDateTime(FromDate),
        Rule = SelectedRule.Value,
        DayLimit = int.TryParse(DayLimitText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var limit) ? limit : 0,
    };

    [RelayCommand]
    private void Remove() => _remove(this);
}

/// <summary>One proposed meter replacement of the preview, with the values the owner can correct before importing.</summary>
public sealed partial class ImportReplacementRowViewModel : ObservableObject
{
    private readonly PlannedReplacement _replacement;
    private readonly PlannedReplacementRegister _line;

    /// <summary>Creates the row.</summary>
    /// <param name="replacement">The proposal.</param>
    /// <param name="line">The register of the proposal that is shown.</param>
    /// <param name="positionLabel">Name of the position (meter and place).</param>
    /// <param name="localizer">Texts.</param>
    public ImportReplacementRowViewModel(PlannedReplacement replacement, PlannedReplacementRegister line, string positionLabel, ILocalizer localizer)
    {
        _replacement = replacement;
        _line = line;
        TitleText = localizer.Get("ImportReplacementTitle", positionLabel, replacement.RowNumber);
        DateLabel = localizer["ImportReplacementDate"];
        FinalLabel = localizer["ImportReplacementFinal"];
        InitialLabel = localizer["ImportReplacementInitial"];
        SerialLabel = localizer["ImportReplacementSerial"];
        _date = replacement.Date.ToDateTime(TimeOnly.MinValue);
        _finalText = Format(line.FinalReading.Value);
        _initialText = Format(line.InitialReading.Value);
        _serialText = replacement.NewMeter.SerialNumber ?? string.Empty;
        var flags = new List<string>();
        if (line.FinalEstimated)
        {
            flags.Add(localizer["ImportReplacementFinalEstimated"]);
        }

        if (line.InitialEstimated)
        {
            flags.Add(localizer["ImportReplacementInitialEstimated"]);
        }

        EstimatedText = string.Join(" · ", flags);
        OriginalDate = replacement.Date;
        OriginalFinal = line.FinalReading.Value;
        OriginalInitial = line.InitialReading.Value;
        OriginalSerial = replacement.NewMeter.SerialNumber ?? string.Empty;
    }

    [ObservableProperty]
    private DateTime _date;

    [ObservableProperty]
    private string _finalText;

    [ObservableProperty]
    private string _initialText;

    [ObservableProperty]
    private string _serialText;

    /// <summary>"Cold water — Bathroom · row 10".</summary>
    public string TitleText { get; }

    public string DateLabel { get; }

    public string FinalLabel { get; }

    public string InitialLabel { get; }

    public string SerialLabel { get; }

    /// <summary>Says which of the values were estimated (empty when both were read from the sheet).</summary>
    public string EstimatedText { get; }

    /// <summary>True when some value was estimated.</summary>
    public bool HasEstimate => EstimatedText.Length > 0;

    /// <summary>The position (register of the first meter of the chain).</summary>
    public Guid PositionRegisterId => _line.PositionRegisterId;

    /// <summary>Sheet row of the replacement.</summary>
    public int RowNumber => _replacement.RowNumber;

    internal DateOnly OriginalDate { get; }

    internal decimal OriginalFinal { get; }

    internal decimal OriginalInitial { get; }

    internal string OriginalSerial { get; }

    /// <summary>The correction the owner typed, or null when nothing differs from the proposal. Unreadable numbers count as not typed.</summary>
    public ReplacementEdit? ToEdit()
    {
        var date = DateOnly.FromDateTime(Date);
        decimal? final = DecimalInput.TryParse(FinalText, CultureInfo.CurrentCulture, out var f) && f != OriginalFinal ? f : null;
        decimal? initial = DecimalInput.TryParse(InitialText, CultureInfo.CurrentCulture, out var i) && i != OriginalInitial ? i : null;
        var serial = SerialText.Trim();
        var serialChanged = serial.Length > 0 && serial != OriginalSerial;
        if (date == OriginalDate && final is null && initial is null && !serialChanged)
        {
            return null;
        }

        return new ReplacementEdit(RowNumber, PositionRegisterId, date == OriginalDate ? null : date, final, initial, serialChanged ? serial : null);
    }

    private static string Format(decimal value) => DecimalInput.Format(value, CultureInfo.CurrentCulture, 3);
}

/// <summary>The dialog in which the owner moves one sheet row to another period, lets its bill cover several months or adds a remark.</summary>
public sealed partial class ImportRowOverrideEditorViewModel : EditorDialogViewModel
{
    private readonly DateOnly _rowDate;
    private readonly YearMonth _ruleBasedPeriod;

    /// <summary>Creates the dialog.</summary>
    /// <param name="localizer">Texts.</param>
    /// <param name="row">The sheet row.</param>
    public ImportRowOverrideEditorViewModel(ILocalizer localizer, PlannedRow row)
        : base(localizer)
    {
        ArgumentNullException.ThrowIfNull(row);

        _rowDate = row.RowDate ?? throw new ArgumentException("The row has no date.", nameof(row));
        _ruleBasedPeriod = row.RuleBasedPeriod ?? YearMonth.From(_rowDate);
        Title = localizer.Get("ImportRowOverrideTitle", row.RowNumber, _rowDate.ToString("d", CultureInfo.CurrentCulture));
        PeriodLabel = localizer["ImportOverridePeriod"];
        CoversLabel = localizer["ImportOverrideCovers"];
        UntilLabel = localizer["ImportOverrideUntil"];
        RemarkLabel = localizer["ImportOverrideRemark"];
        ClearText = localizer["ImportOverrideClear"];
        RuleHint = localizer.Get("ImportOverrideRuleHint", _ruleBasedPeriod.ToString());
        _rangeError = string.Empty;
        _localizer = localizer;

        var current = row.Override;
        _period = (current?.Period ?? row.Period?.FirstDay ?? _ruleBasedPeriod.FirstDay).ToDateTime(TimeOnly.MinValue);
        _covers = current?.CoversUntil is not null;
        _until = (current?.CoversUntil ?? DateOnly.FromDateTime(_period.AddMonths(1))).ToDateTime(TimeOnly.MinValue);
        _remark = current?.Remark ?? string.Empty;
    }

    private readonly ILocalizer _localizer;

    [ObservableProperty]
    private DateTime _period;

    [ObservableProperty]
    private bool _covers;

    [ObservableProperty]
    private DateTime _until;

    [ObservableProperty]
    private string _remark;

    [ObservableProperty]
    private string _rangeError;

    public string PeriodLabel { get; }

    public string CoversLabel { get; }

    public string UntilLabel { get; }

    public string RemarkLabel { get; }

    public string ClearText { get; }

    public string RuleHint { get; }

    /// <summary>The correction to store; null (after Save) means no correction is needed, e.g. after Reset.</summary>
    public ImportRowOverride? Result { get; private set; }

    /// <summary>Removes the correction of the row.</summary>
    [RelayCommand]
    private void Clear()
    {
        Result = null;
        CloseSaved();
    }

    protected override bool TryApply()
    {
        RangeError = string.Empty;
        var period = YearMonth.From(DateOnly.FromDateTime(Period));
        var until = YearMonth.From(DateOnly.FromDateTime(Until));
        if (Covers && until < period)
        {
            RangeError = _localizer["ImportOverrideRangeInvalid"];
            return false;
        }

        var remark = Remark.Trim();
        Result = new ImportRowOverride
        {
            RowDate = _rowDate,
            Period = period == _ruleBasedPeriod ? null : period.FirstDay,
            CoversUntil = Covers && until > period ? until.FirstDay : null,
            Remark = remark.Length == 0 ? null : (remark.Length > InputLimits.Notes ? remark[..InputLimits.Notes] : remark),
        };
        if (Result.IsEmpty)
        {
            Result = null;
        }

        return true;
    }
}
