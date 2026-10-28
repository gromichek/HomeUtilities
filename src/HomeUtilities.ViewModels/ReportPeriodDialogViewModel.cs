using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Common;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The period dialog of the Reports screen: a preset (this year, last 12 months, last year, all) or a custom range with a
/// "from" and a "to" month picker.
/// </summary>
public sealed partial class ReportPeriodDialogViewModel : EditorDialogViewModel
{
    private readonly ILocalizer _localizer;

    [ObservableProperty]
    private IReadOnlyList<ReportOptionViewModel> _presets = [];

    [ObservableProperty]
    private ReportPreset _preset;

    [ObservableProperty]
    private int _fromIndex;

    [ObservableProperty]
    private int _toIndex;

    [ObservableProperty]
    private string _error = string.Empty;

    /// <summary>Creates the dialog.</summary>
    /// <param name="localizer">Texts.</param>
    /// <param name="preset">The current preset.</param>
    /// <param name="from">First month of the current range (the start of the custom range).</param>
    /// <param name="to">Last month of the current range.</param>
    /// <param name="earliest">First month the pickers offer (widened to include the range).</param>
    /// <param name="latest">Last month the pickers offer (widened to include the range).</param>
    public ReportPeriodDialogViewModel(ILocalizer localizer, ReportPreset preset, YearMonth from, YearMonth to, YearMonth earliest, YearMonth latest)
        : base(localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        _localizer = localizer;
        Title = localizer["PeriodLabel"];
        FromLabel = localizer["ReportFromLabel"];
        ToLabel = localizer["ReportToLabel"];
        OkText = localizer["Dialog_Ok"];

        var first = new[] { earliest, from, to }.Min();
        var last = new[] { latest, from, to }.Max();
        var months = new List<YearMonth>();
        for (var month = first; month <= last; month = month.AddMonths(1))
        {
            months.Add(month);
        }

        Months = months;
        MonthTexts = [.. months.Select(m => $"{m.Month:00}.{m.Year}")];
        _preset = preset;
        _fromIndex = months.IndexOf(from);
        _toIndex = months.IndexOf(to);
        BuildPresets();
    }

    /// <summary>Label of the "from" picker.</summary>
    public string FromLabel { get; }

    /// <summary>Label of the "to" picker.</summary>
    public string ToLabel { get; }

    /// <summary>Text of the confirm button.</summary>
    public string OkText { get; }

    /// <summary>The months the pickers offer.</summary>
    public IReadOnlyList<YearMonth> Months { get; }

    /// <summary>The months as <c>MM.yyyy</c>, for the pickers.</summary>
    public IReadOnlyList<string> MonthTexts { get; }

    /// <summary>True while "Custom…" is chosen (the month pickers are shown).</summary>
    public bool IsCustom => Preset == ReportPreset.Custom;

    /// <summary>The chosen range of a custom period.</summary>
    public (YearMonth From, YearMonth To) Range => (Months[Math.Max(FromIndex, 0)], Months[Math.Max(ToIndex, 0)]);

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
