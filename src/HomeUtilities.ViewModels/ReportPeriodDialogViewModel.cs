using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Common;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The period dialog of the Reports screen: a preset (this year, last 12 months, last year, all) or a custom range with a
/// "from" and a "to" month picker.
/// </summary>
public sealed partial class ReportPeriodDialogViewModel : EditorDialogViewModel, IChoiceDialog
{
    private readonly ILocalizer _localizer;

    [ObservableProperty]
    private IReadOnlyList<ReportOptionViewModel> _presets = [];

    [ObservableProperty]
    private ReportPreset _preset;

    [ObservableProperty]
    private DateOnly _fromDate;

    [ObservableProperty]
    private DateOnly _toDate;

    [ObservableProperty]
    private string _error = string.Empty;

    /// <summary>Creates the dialog.</summary>
    /// <param name="localizer">Texts.</param>
    /// <param name="preset">The current preset.</param>
    /// <param name="from">First month of the current range (the start of the custom range).</param>
    /// <param name="to">Last month of the current range.</param>
    /// <param name="earliest">First month the calendars offer (widened to include the range).</param>
    /// <param name="latest">Last month the calendars offer (widened to include the range).</param>
    public ReportPeriodDialogViewModel(ILocalizer localizer, ReportPreset preset, YearMonth from, YearMonth to, YearMonth earliest, YearMonth latest)
        : base(localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        _localizer = localizer;
        Title = localizer["PeriodLabel"];
        FromLabel = localizer["ReportFromLabel"];
        ToLabel = localizer["ReportToLabel"];
        OkText = localizer["Dialog_Ok"];

        MinimumMonth = new[] { earliest, from, to }.Min();
        MaximumMonth = new[] { latest, from, to }.Max();
        _preset = preset;
        _fromDate = from.FirstDay;
        _toDate = to.FirstDay;
        BuildPresets();
    }

    /// <inheritdoc />
    public int RowCountHint => 10;

    /// <summary>Label of the "from" picker.</summary>
    public string FromLabel { get; }

    /// <summary>Label of the "to" picker.</summary>
    public string ToLabel { get; }

    /// <summary>Text of the confirm button.</summary>
    public string OkText { get; }

    /// <summary>The first month the calendars offer.</summary>
    public YearMonth MinimumMonth { get; }

    /// <summary>The last month the calendars offer.</summary>
    public YearMonth MaximumMonth { get; }

    /// <summary>The first day the date pickers allow.</summary>
    public DateOnly MinimumDate => MinimumMonth.FirstDay;

    /// <summary>The last day the date pickers allow.</summary>
    public DateOnly MaximumDate => MaximumMonth.LastDay;

    /// <summary>
    /// The months the calendars offer for the data of the Reports screen: from the first month with a bill or a reading
    /// (10 years back from <paramref name="current"/> if there is none) to the later of <paramref name="current"/> and the
    /// last bill's period (bills of prepaid services may lie in the future).
    /// </summary>
    /// <param name="earliestData">First month with data, or null.</param>
    /// <param name="latestBill">Period of the last bill, or null.</param>
    /// <param name="current">The current month.</param>
    public static (YearMonth Earliest, YearMonth Latest) PickerRange(YearMonth? earliestData, YearMonth? latestBill, YearMonth current)
    {
        var latest = latestBill is { } bill && bill > current ? bill : current;
        var earliest = earliestData ?? current.AddMonths(-120);
        return (earliest < latest ? earliest : latest, latest);
    }

    /// <summary>True while "Custom…" is chosen (the month pickers are shown).</summary>
    public bool IsCustom => Preset == ReportPreset.Custom;

    /// <summary>The chosen range of a custom period.</summary>
    public (YearMonth From, YearMonth To) Range => (YearMonth.From(FromDate), YearMonth.From(ToDate));

    /// <summary>OK.</summary>
    [RelayCommand]
    private void Confirm()
    {
        if (TryApply())
        {
            CloseSaved();
        }
    }

    protected override bool TryApply()
    {
        if (IsCustom && Range.To < Range.From)
        {
            Error = _localizer["ReportPeriodInvalid"];
            return false;
        }

        Error = string.Empty;
        return true;
    }

    partial void OnPresetChanged(ReportPreset value)
    {
        OnPropertyChanged(nameof(IsCustom));
        Error = string.Empty;
        BuildPresets();
    }

    private void BuildPresets()
    {
        ReportPreset[] all = [ReportPreset.ThisYear, ReportPreset.Last12Months, ReportPreset.LastYear, ReportPreset.All, ReportPreset.Custom];
        Presets =
        [
            .. all.Select(p => new ReportOptionViewModel(_localizer["ReportPreset" + p], p == Preset, o => Preset = (ReportPreset)o.Value!) { Value = p }),
        ];
    }
}
