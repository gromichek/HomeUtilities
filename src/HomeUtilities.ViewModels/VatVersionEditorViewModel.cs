using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>Dialog that adds or edits one percentage of a VAT catalog entry.</summary>
public sealed partial class VatVersionEditorViewModel : EditorDialogViewModel
{
    private readonly ILocalizer _localizer;
    private readonly Func<VatRateVersion, bool> _isDuplicate;
    private readonly VatRateVersion? _existing;

    [ObservableProperty]
    private DateOnly _validFrom;

    [ObservableProperty]
    private string _percentText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidFromError), nameof(PercentError))]
    private IReadOnlyDictionary<string, string> _errors = new Dictionary<string, string>();

    [ObservableProperty]
    private string _validFromLabel = string.Empty;

    [ObservableProperty]
    private string _percentLabel = string.Empty;

    /// <param name="localizer">Localizer.</param>
    /// <param name="clock">Clock for the default valid-from date.</param>
    /// <param name="existing">The version to edit, or null to add one.</param>
    /// <param name="isDuplicate">Tells whether another version already starts on the candidate's date.</param>
    public VatVersionEditorViewModel(ILocalizer localizer, IClock clock, VatRateVersion? existing, Func<VatRateVersion, bool> isDuplicate)
        : base(localizer)
    {
        _localizer = localizer;
        _existing = existing;
        _isDuplicate = isDuplicate;
        IsNew = existing is null;
        ValidFrom = existing?.ValidFrom ?? clock.Today;
        PercentText = existing is null ? string.Empty : DecimalInput.Format(existing.Percent, CultureInfo.CurrentCulture, 4);
        RefreshTexts();
    }

    public bool IsNew { get; }

    public string ValidFromError => Lookup(nameof(VatRateVersion.ValidFrom));

    public string PercentError => Lookup(nameof(VatRateVersion.Percent));

    /// <summary>The validated version; meaningful only after <see cref="EditorDialogViewModel.Saved"/>.</summary>
    public VatRateVersion? Result { get; private set; }

    protected override bool TryApply()
    {
        var errors = new List<ValidationError>();
        var percent = 0m;
        if (!DecimalInput.TryParse(PercentText, CultureInfo.CurrentCulture, out percent))
        {
            errors.Add(new(CommonErrorCodes.NumberInvalid, nameof(VatRateVersion.Percent)));
        }
        else if (percent < 0m || percent > 100m)
        {
            errors.Add(new(ErrorCodes.VatPercentOutOfRange, nameof(VatRateVersion.Percent)));
        }

        var candidate = new VatRateVersion
        {
            Id = _existing?.Id ?? Guid.CreateVersion7(),
            CreatedAt = _existing?.CreatedAt ?? default,
            UpdatedAt = _existing?.UpdatedAt ?? default,
            VatRateId = _existing?.VatRateId ?? Guid.Empty,
            ValidFrom = ValidFrom,
            Percent = percent,
        };
        if (_isDuplicate(candidate))
        {
            errors.Add(new(ErrorCodes.DuplicateValidFrom, nameof(VatRateVersion.ValidFrom), [candidate.ValidFrom]));
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
        Title = _localizer[IsNew ? "VatVersionNew" : "VatVersionEdit"];
        ValidFromLabel = _localizer["VatVersionValidFrom"];
        PercentLabel = _localizer["VatVersionPercent"];
    }

    private string Lookup(string key) => Errors.TryGetValue(key, out var message) ? message : string.Empty;
}
