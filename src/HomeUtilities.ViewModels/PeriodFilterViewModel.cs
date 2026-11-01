using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The period filter control of the Bills and Payments screens: a scope drop-down (All years / Year / Month), ‹ › buttons
/// and the label ("2026", "October 2026"). The filter value itself lives in the screen's filter state (read/write
/// delegates), so it is remembered for the session.
/// </summary>
public sealed partial class PeriodFilterViewModel : ObservableObject
{
    private readonly Func<PeriodFilter?> _read;
    private readonly Action<PeriodFilter> _write;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;
    private readonly IDialogService _dialogs;
    private readonly Action _changed;
    private readonly CultureInfo? _culture;

    /// <param name="read">Reads the remembered filter; null = the screen was not used yet (current year).</param>
    /// <param name="write">Remembers the filter.</param>
    /// <param name="clock">Supplies today's date for the defaults.</param>
    /// <param name="localizer">Supplies the texts.</param>
    /// <param name="dialogs">Shows the choice dialog of the scope drop-down.</param>
    /// <param name="changed">Called after the user changed the filter, to rebuild the list.</param>
    /// <param name="culture">Culture for the month name; null = the current culture of the app.</param>
    public PeriodFilterViewModel(
        Func<PeriodFilter?> read,
        Action<PeriodFilter> write,
        IClock clock,
        ILocalizer localizer,
        IDialogService dialogs,
        Action changed,
        CultureInfo? culture = null)
    {
        _read = read;
        _write = write;
        _clock = clock;
        _localizer = localizer;
        _dialogs = dialogs;
        _changed = changed;
        _culture = culture;
    }

    /// <summary>The filter in force.</summary>
    public PeriodFilter Current => _read() ?? PeriodFilter.Default(_clock.Today);

    public bool IsAllYearsScope => Current.Scope == PeriodScope.AllYears;

    public bool IsYearScope => Current.Scope == PeriodScope.Year;

    public bool IsMonthScope => Current.Scope == PeriodScope.Month;

    /// <summary>The ‹ › buttons are hidden in "all years" mode.</summary>
    public bool ShowStepper => Current.Scope != PeriodScope.AllYears;

    /// <summary>"2026" in year mode, "October 2026" in month mode, "All years" otherwise.</summary>
    public string Label
    {
        get
        {
            var current = Current;
            switch (current.Scope)
            {
                case PeriodScope.Year:
                    return current.Year.ToString(CultureInfo.InvariantCulture);
                case PeriodScope.Month:
                    var culture = _culture ?? CultureInfo.CurrentCulture;
                    var text = current.MonthValue.FirstDay.ToString("MMMM yyyy", culture);
                    return text.Length == 0 ? text : char.ToUpper(text[0], culture) + text[1..];
                default:
                    return AllYearsText;
            }
        }
    }

    /// <summary>The value shown in the scope drop-down: the name of the current scope.</summary>
    public string ScopeText => Current.Scope switch
    {
        PeriodScope.Year => YearText,
        PeriodScope.Month => MonthText,
        _ => AllYearsText,
    };

    public string PeriodLabel => _localizer["PeriodLabel"];

    public string AllYearsText => _localizer["PeriodAllYears"];

    public string YearText => _localizer["PeriodYear"];

    public string MonthText => _localizer["PeriodMonth"];

    public string PreviousText => _localizer[IsMonthScope ? "PeriodPreviousMonth" : "PeriodPreviousYear"];

    public string NextText => _localizer[IsMonthScope ? "PeriodNextMonth" : "PeriodNextYear"];

    /// <summary>Sets the filter without rebuilding the list (the caller does that), e.g. "All bills" opening all years.</summary>
    public void Set(PeriodFilter filter)
    {
        _write(filter);
        Refresh();
    }

    /// <summary>Raises change notifications for everything (also after a language switch).</summary>
    public void Refresh() => OnPropertyChanged(string.Empty);

    /// <summary>Opens the choice dialog of the scope drop-down (All years / Year / Month).</summary>
    [RelayCommand]
    private async Task ChooseScopeAsync()
    {
        var options = new (string Name, PeriodScope Scope)[]
        {
            (AllYearsText, PeriodScope.AllYears),
            (YearText, PeriodScope.Year),
            (MonthText, PeriodScope.Month),
        };
        var picked = await _dialogs.ChooseAsync(PeriodLabel, [.. options.Select(o => o.Name)]);
        if (picked is null || options.FirstOrDefault(o => o.Name == picked) is not { Name: not null } option)
        {
            return;
        }

        if (option.Scope != Current.Scope)
        {
            Change(Current.WithScope(option.Scope, _clock.Today));
        }
    }

    [RelayCommand]
    private void ShowAllYears() => Change(Current.WithScope(PeriodScope.AllYears, _clock.Today));

    [RelayCommand]
    private void ShowYear() => Change(Current.WithScope(PeriodScope.Year, _clock.Today));

    [RelayCommand]
    private void ShowMonth() => Change(Current.WithScope(PeriodScope.Month, _clock.Today));

    [RelayCommand]
    private void Previous() => Change(Current.Step(-1));

    [RelayCommand]
    private void Next() => Change(Current.Step(1));

    private void Change(PeriodFilter filter)
    {
        Set(filter);
        _changed();
    }
}
