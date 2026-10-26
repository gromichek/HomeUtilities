using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Taxes;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>A due date row of the yearly tax dialog.</summary>
public sealed partial class TaxDueDateRowViewModel : ObservableObject
{
    [ObservableProperty]
    private DateOnly _date;

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private string _error = string.Empty;
}

/// <summary>What the "Enter yearly tax" dialog returns.</summary>
/// <param name="Year">The tax year.</param>
/// <param name="Total">The yearly amount.</param>
/// <param name="DueDates">One due date per installment, ascending.</param>
public sealed record TaxYearInput(int Year, decimal Total, IReadOnlyList<DateOnly> DueDates);

/// <summary>
/// Dialog "Enter yearly tax": the year, the yearly amount and the number of installments (1 / 2 / 4) with their due dates,
/// pre-filled with the Latvian defaults and editable. Changing the year or the count resets the dates to the defaults.
/// </summary>
public sealed partial class TaxYearEditorViewModel : EditorDialogViewModel
{
    private readonly ILocalizer _localizer;
    private readonly string _currencySymbol;
    private bool _syncing;

    [ObservableProperty]
    private string _yearText = string.Empty;

    [ObservableProperty]
    private string _amountText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCountIndex))]
    private IReadOnlyList<EnumOption> _counts = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCountIndex))]
    private EnumOption? _selectedCount;

    [ObservableProperty]
    private IReadOnlyList<TaxDueDateRowViewModel> _dueDates = [];

    [ObservableProperty]
    private string _previewText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(YearError), nameof(AmountError), nameof(CountError))]
    private IReadOnlyDictionary<string, string> _errors = new Dictionary<string, string>();

    [ObservableProperty]
    private string _yearLabel = string.Empty;

    [ObservableProperty]
    private string _amountLabel = string.Empty;

    [ObservableProperty]
    private string _countLabel = string.Empty;

    [ObservableProperty]
    private string _dueDatesLabel = string.Empty;

    [ObservableProperty]
    private string _dueDatesHint = string.Empty;

    public TaxYearEditorViewModel(ILocalizer localizer, IClock clock, string currencySymbol)
        : base(localizer)
    {
        _localizer = localizer;
        _currencySymbol = currencySymbol;
        Counts = [.. TaxInstallmentPlanner.SupportedCounts.Select(c => new EnumOption(c, c.ToString(CultureInfo.CurrentCulture)))];
        _syncing = true;
        SelectedCount = Counts.First(c => c.Value.Equals(4));
        YearText = clock.Today.Year.ToString(CultureInfo.InvariantCulture);
        _syncing = false;
        ResetDueDates();
        RefreshTexts();
    }

    public int SelectedCountIndex
    {
        get => SelectedCount is null ? -1 : Counts.ToList().IndexOf(SelectedCount);
        set
        {
            if (value >= 0 && value < Counts.Count)
            {
                SelectedCount = Counts[value];
            }
        }
    }

    public string YearError => Lookup("Year");

    public string AmountError => Lookup("Total");

    public string CountError => Lookup("Count");

    /// <summary>The validated input; meaningful only after <see cref="EditorDialogViewModel.Saved"/>.</summary>
    public TaxYearInput? Result { get; private set; }

    partial void OnYearTextChanged(string value) => ResetDueDates();

    partial void OnSelectedCountChanged(EnumOption? value) => ResetDueDates();

    partial void OnAmountTextChanged(string value) => UpdatePreview();

    protected override bool TryApply()
    {
        var errors = new List<ValidationError>();
        var year = ParseYear();
        if (year is null)
        {
            errors.Add(new(CommonErrorCodes.NumberInvalid, "Year"));
        }

        if (!DecimalInput.TryParse(AmountText, CultureInfo.CurrentCulture, out var total))
        {
            errors.Add(new(CommonErrorCodes.NumberInvalid, "Total"));
        }

        var dates = DueDates.Select(r => r.Date).ToList();
        foreach (var row in DueDates)
        {
            row.Error = string.Empty;
        }

        if (errors.Count == 0)
        {
            foreach (var error in TaxInstallmentPlanner.Validate(year!.Value, total, dates).Errors)
            {
                if (error.Field is { } field && field.StartsWith("DueDates[", StringComparison.Ordinal)
                    && int.Parse(field.AsSpan(9, field.IndexOf(']', StringComparison.Ordinal) - 9), CultureInfo.InvariantCulture) is var index
                    && index < DueDates.Count)
                {
                    DueDates[index].Error = ValidationText.For(_localizer, error);
                }
                else
                {
                    errors.Add(error);
                }
            }

            if (DueDates.Any(r => r.Error.Length > 0))
            {
                errors.Add(new(ErrorCodes.TaxDueDatesNotAscending, "DueDates"));
            }
        }

        Errors = errors
            .Where(e => e.Field != "DueDates")
            .GroupBy(e => e.Field ?? string.Empty)
            .ToDictionary(g => g.Key, g => string.Join("\n", g.Select(e => ValidationText.For(_localizer, e))));
        if (errors.Count > 0)
        {
            return false;
        }

        Result = new TaxYearInput(year!.Value, total, dates);
        return true;
    }

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        Title = _localizer["TaxYearNew"];
        YearLabel = _localizer["TaxYearLabel"];
        AmountLabel = _localizer["TaxYearAmount"];
        CountLabel = _localizer["TaxYearCount"];
        DueDatesLabel = _localizer["TaxYearDueDates"];
        DueDatesHint = _localizer["TaxYearDueDatesHint"];
        for (var i = 0; i < DueDates.Count; i++)
        {
            DueDates[i].Label = _localizer.Get("TaxYearInstallmentNumber", i + 1);
        }

        UpdatePreview();
    }

    private int? ParseYear() =>
        int.TryParse(YearText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var year) && year is >= 1900 and <= 2200
            ? year
            : null;

    /// <summary>The due dates of the chosen year and count, back to the Latvian defaults.</summary>
    private void ResetDueDates()
    {
        if (_syncing || SelectedCount is null)
        {
            return;
        }

        var count = (int)SelectedCount.Value;
        var year = ParseYear();
        if (year is null)
        {
            DueDates = [];
            UpdatePreview();
            return;
        }

        var rows = new List<TaxDueDateRowViewModel>();
        foreach (var (date, i) in TaxInstallmentPlanner.DefaultDueDates(year.Value, count).Select((d, i) => (d, i)))
        {
            rows.Add(new TaxDueDateRowViewModel { Date = date, Label = _localizer.Get("TaxYearInstallmentNumber", i + 1) });
        }

        DueDates = rows;
        UpdatePreview();
    }

    /// <summary>"25,25 € · 25,25 € · 25,25 € · 25,25 €": what each installment will be.</summary>
    private void UpdatePreview()
    {
        PreviewText = DueDates.Count > 0 && DecimalInput.TryParse(AmountText, CultureInfo.CurrentCulture, out var total) && total > 0
            ? string.Join(" · ", TaxInstallmentPlanner.Split(total, DueDates.Count).Select(a => BillText.Money(a, _currencySymbol)))
            : string.Empty;
    }

    private string Lookup(string key) => Errors.TryGetValue(key, out var message) ? message : string.Empty;
}
