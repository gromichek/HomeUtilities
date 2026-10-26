using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Equalized;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>Dialog that adds or edits one version of an equalized plan (valid from, optional valid to, monthly amount).</summary>
public sealed partial class EqualizedPlanEditorViewModel : EditorDialogViewModel
{
    private readonly ILocalizer _localizer;
    private readonly Func<EqualizedPlan, ValidationResult> _check;
    private readonly EqualizedPlan? _existing;

    [ObservableProperty]
    private DateOnly _validFrom;

    [ObservableProperty]
    private bool _hasValidTo;

    [ObservableProperty]
    private DateOnly _validTo;

    [ObservableProperty]
    private string _amountText = string.Empty;

    [ObservableProperty]
    private string _note = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidFromError), nameof(ValidToError), nameof(AmountError), nameof(NoteError))]
    private IReadOnlyDictionary<string, string> _errors = new Dictionary<string, string>();

    [ObservableProperty]
    private string _validFromLabel = string.Empty;

    [ObservableProperty]
    private string _hasValidToLabel = string.Empty;

    [ObservableProperty]
    private string _validToLabel = string.Empty;

    [ObservableProperty]
    private string _amountLabel = string.Empty;

    [ObservableProperty]
    private string _noteLabel = string.Empty;

    /// <param name="localizer">Texts.</param>
    /// <param name="clock">Today, the default start of a new plan.</param>
    /// <param name="existing">The plan to edit, or null to add a new one.</param>
    /// <param name="check">Validates a candidate together with the other plans of the contract.</param>
    public EqualizedPlanEditorViewModel(
        ILocalizer localizer, IClock clock, EqualizedPlan? existing, Func<EqualizedPlan, ValidationResult> check)
        : base(localizer)
    {
        _localizer = localizer;
        _check = check;
        _existing = existing;
        IsNew = existing is null;

        ValidFrom = existing?.ValidFrom ?? YearMonth.From(clock.Today).AddMonths(1).FirstDay;
        HasValidTo = existing?.ValidTo is not null;
        ValidTo = existing?.ValidTo ?? ValidFrom;
        AmountText = existing is null ? string.Empty : AmountEditor.FormatMoney(existing.MonthlyAmount);
        Note = existing?.Notes ?? string.Empty;
        RefreshTexts();
    }

    public bool IsNew { get; }

    public string ValidFromError => Lookup(nameof(EqualizedPlan.ValidFrom));

    public string ValidToError => Lookup(nameof(EqualizedPlan.ValidTo));

    public string AmountError => Lookup(nameof(EqualizedPlan.MonthlyAmount));

    public string NoteError => Lookup(nameof(EqualizedPlan.Notes));

    /// <summary>The validated plan; meaningful only after <see cref="EditorDialogViewModel.Saved"/>.</summary>
    public EqualizedPlan? Result { get; private set; }

    protected override bool TryApply()
    {
        var errors = new List<ValidationError>();
        if (!DecimalInput.TryParse(AmountText, CultureInfo.CurrentCulture, out var amount))
        {
            errors.Add(new(CommonErrorCodes.NumberInvalid, nameof(EqualizedPlan.MonthlyAmount)));
        }

        var candidate = new EqualizedPlan
        {
            ValidFrom = ValidFrom,
            ValidTo = HasValidTo ? ValidTo : null,
            MonthlyAmount = amount,
            Notes = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
        };
        if (_existing is not null)
        {
            candidate.Id = _existing.Id;
            candidate.ContractId = _existing.ContractId;
            candidate.CreatedAt = _existing.CreatedAt;
        }

        if (errors.Count == 0)
        {
            errors.AddRange(_check(candidate).Errors);
        }

        Errors = errors
            .GroupBy(e => e.Field ?? string.Empty)
            .ToDictionary(g => g.Key, g => string.Join("\n", g.Select(e => ValidationText.For(_localizer, e))));
        if (errors.Count > 0)
        {
            return false;
        }

        Result = candidate;
        return true;
    }

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        Title = _localizer[IsNew ? "EqPlanNew" : "EqPlanEdit"];
        ValidFromLabel = _localizer["EqPlanValidFrom"];
        HasValidToLabel = _localizer["EqPlanHasValidTo"];
        ValidToLabel = _localizer["EqPlanValidTo"];
        AmountLabel = _localizer["EqPlanAmount"];
        NoteLabel = _localizer["EqPlanNote"];
    }

    private string Lookup(string key) => Errors.TryGetValue(key, out var message) ? message : string.Empty;
}
