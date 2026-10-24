using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>Dialog that adds or edits one price of a tariff component, optionally as a stepped price.</summary>
public sealed partial class TariffRateEditorViewModel : EditorDialogViewModel
{
    /// <summary>Largest price or threshold the dialog accepts; keeps examples and calculations inside the decimal range.</summary>
    public const decimal MaxNumber = 1_000_000_000m;

    private const int MaxDecimals = 6;
    private const string RatePrefix = "Rates[0].";

    private readonly ILocalizer _localizer;
    private readonly Func<TariffRate, bool> _isDuplicate;
    private readonly TariffRate? _existing;
    private readonly Currency _currency;
    private readonly List<TariffTierRowViewModel> _tiers = [];

    [ObservableProperty]
    private DateOnly _validFrom;

    [ObservableProperty]
    private string _priceText = string.Empty;

    [ObservableProperty]
    private bool _priceIncludesVat;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFlatPrice))]
    private bool _isStepped;

    [ObservableProperty]
    private IReadOnlyList<TariffTierRowViewModel> _tierRows = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTierPeriodIndex))]
    private IReadOnlyList<EnumOption> _tierPeriods = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTierPeriodIndex))]
    private EnumOption? _selectedTierPeriod;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidFromError), nameof(PriceError), nameof(TiersError), nameof(TierPeriodError), nameof(NotesError))]
    private IReadOnlyDictionary<string, string> _errors = new Dictionary<string, string>();

    [ObservableProperty]
    private string _validFromLabel = string.Empty;

    [ObservableProperty]
    private string _priceLabel = string.Empty;

    [ObservableProperty]
    private string _priceIncludesVatLabel = string.Empty;

    [ObservableProperty]
    private string _currencyLabel = string.Empty;

    [ObservableProperty]
    private string _currencyText = string.Empty;

    [ObservableProperty]
    private string _steppedLabel = string.Empty;

    [ObservableProperty]
    private string _tiersHint = string.Empty;

    [ObservableProperty]
    private string _addTierText = string.Empty;

    [ObservableProperty]
    private string _tierPeriodLabel = string.Empty;

    [ObservableProperty]
    private string _notesLabel = string.Empty;

    /// <param name="localizer">Localizer.</param>
    /// <param name="clock">Clock for the default valid-from date.</param>
    /// <param name="existing">The price to edit, or null to add one.</param>
    /// <param name="currency">Currency of the contract; shown read-only.</param>
    /// <param name="allowTiers">Whether a stepped price may be entered (not for a net-export component).</param>
    /// <param name="template">The newest price, to pre-fill a new one; may be null.</param>
    /// <param name="isDuplicate">Tells whether another price already starts on the candidate's date.</param>
    public TariffRateEditorViewModel(
        ILocalizer localizer,
        IClock clock,
        TariffRate? existing,
        Currency currency,
        bool allowTiers,
        TariffRate? template,
        Func<TariffRate, bool> isDuplicate)
        : base(localizer)
    {
        _localizer = localizer;
        _existing = existing;
        _currency = currency;
        _isDuplicate = isDuplicate;
        AllowTiers = allowTiers;
        IsNew = existing is null;

        TierPeriods = EnumText.Options<TierPeriod>(localizer);
        var source = existing ?? template;
        ValidFrom = existing?.ValidFrom ?? clock.Today;
        PriceIncludesVat = source?.PriceIncludesVat ?? false;
        Notes = existing?.Notes ?? string.Empty;
        SelectedTierPeriod = TierPeriods.First(o => o.Value.Equals(source?.TierPeriod ?? TierPeriod.Month));
        if (existing is not null)
        {
            PriceText = existing.Tiers.Count > 0 ? string.Empty : FormatNumber(existing.Price);
        }

        if (source is { Tiers.Count: > 0 } && allowTiers)
        {
            foreach (var tier in source.Tiers)
            {
                AddTierRow(tier.UpToQuantity is { } upTo ? FormatNumber(upTo) : string.Empty, FormatNumber(tier.Price));
            }

            IsStepped = existing is not null;
        }

        RefreshTexts();
    }

    public bool IsNew { get; }

    /// <summary>Whether the stepped-price switch is offered.</summary>
    public bool AllowTiers { get; }

    public bool ShowFlatPrice => !IsStepped;

    public int SelectedTierPeriodIndex
    {
        get => SelectedTierPeriod is null ? -1 : TierPeriods.ToList().IndexOf(SelectedTierPeriod);
        set
        {
            if (value >= 0 && value < TierPeriods.Count)
            {
                SelectedTierPeriod = TierPeriods[value];
            }
        }
    }

    public string ValidFromError => Lookup(nameof(TariffRate.ValidFrom));

    public string PriceError => Lookup(nameof(TariffRate.Price));

    public string TiersError => Lookup(nameof(TariffRate.Tiers));

    public string TierPeriodError => Lookup(nameof(TariffRate.TierPeriod));

    public string NotesError => Lookup(nameof(TariffRate.Notes));

    /// <summary>The validated price; meaningful only after <see cref="EditorDialogViewModel.Saved"/>.</summary>
    public TariffRate? Result { get; private set; }

    partial void OnIsSteppedChanged(bool value)
    {
        if (value && _tiers.Count == 0)
        {
            AddTierRow(string.Empty, PriceText);
            AddTierRow(string.Empty, string.Empty);
        }
    }

    [RelayCommand]
    private void AddTier()
    {
        AddTierRow(string.Empty, string.Empty);
    }

    protected override bool TryApply()
    {
        var errors = new List<ValidationError>();
        var price = 0m;
        if (!IsStepped)
        {
            price = ParseNumber(PriceText, nameof(TariffRate.Price), errors);
        }

        var tiers = new List<TariffTier>();
        if (IsStepped)
        {
            var tierErrors = new List<ValidationError>();
            foreach (var row in _tiers)
            {
                decimal? upTo = null;
                if (!string.IsNullOrWhiteSpace(row.UpToText))
                {
                    upTo = ParseNumber(row.UpToText, nameof(TariffRate.Tiers), tierErrors);
                }

                tiers.Add(new TariffTier(upTo, ParseNumber(row.PriceText, nameof(TariffRate.Tiers), tierErrors)));
            }

            errors.AddRange(tierErrors.DistinctBy(e => e.Code));
            price = tiers.Count > 0 ? tiers[0].Price : 0m;
        }

        var candidate = new TariffRate
        {
            Id = _existing?.Id ?? Guid.CreateVersion7(),
            CreatedAt = _existing?.CreatedAt ?? default,
            UpdatedAt = _existing?.UpdatedAt ?? default,
            ComponentId = _existing?.ComponentId ?? Guid.Empty,
            ValidFrom = ValidFrom,
            Price = price,
            PriceIncludesVat = PriceIncludesVat,
            Currency = _currency,
            Tiers = tiers,
            TierPeriod = IsStepped ? SelectedTierPeriod?.As<TierPeriod>() : null,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
        };

        // Only the rules of one rate matter here; the rest of the component is checked by the component editor.
        var probe = new TariffComponent { ContractId = Guid.CreateVersion7(), Name = "x", VatCode = "x", Rates = [candidate] };
        foreach (var error in TariffComponentValidator.Validate(probe).Errors.Where(e => e.Field?.StartsWith(RatePrefix, StringComparison.Ordinal) == true))
        {
            var field = error.Field![RatePrefix.Length..];
            var key = field.StartsWith(nameof(TariffRate.Tiers), StringComparison.Ordinal) ? nameof(TariffRate.Tiers) : field;
            if (!errors.Any(e => e.Field == key && e.Code == error.Code))
            {
                errors.Add(error with { Field = key });
            }
        }

        if (_isDuplicate(candidate))
        {
            errors.Add(new(ErrorCodes.DuplicateValidFrom, nameof(TariffRate.ValidFrom), [candidate.ValidFrom]));
        }

        Errors = errors
            .GroupBy(e => e.Field ?? string.Empty)
            .ToDictionary(g => g.Key, g => string.Join("\n", g.Select(e => ValidationText.For(_localizer, e)).Distinct()));
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
        var period = SelectedTierPeriod?.Value;
        TierPeriods = EnumText.Options<TierPeriod>(_localizer);
        if (period is not null)
        {
            SelectedTierPeriod = TierPeriods.First(o => o.Value.Equals(period));
        }

        Title = _localizer[IsNew ? "TariffRateNew" : "TariffRateEdit"];
        ValidFromLabel = _localizer["TariffRateValidFrom"];
        PriceLabel = _localizer["TariffRatePrice"];
        PriceIncludesVatLabel = _localizer["TariffRateIncludesVat"];
        CurrencyLabel = _localizer["TariffRateCurrency"];
        CurrencyText = EnumText.Get(_localizer, _currency);
        SteppedLabel = _localizer["TariffRateStepped"];
        TiersHint = _localizer["TariffRateTiersHint"];
        AddTierText = _localizer["TariffRateAddTier"];
        TierPeriodLabel = _localizer["TariffRateTierPeriod"];
        NotesLabel = _localizer["TariffRateNotes"];
        foreach (var row in _tiers)
        {
            Relabel(row);
        }
    }

    private void Relabel(TariffTierRowViewModel row)
    {
        row.RemoveText = _localizer["Delete"];
        row.UpToPlaceholder = _localizer["TariffRateTierUpTo"];
        row.PricePlaceholder = _localizer["TariffRateTierPrice"];
        row.AndAboveText = _localizer["TariffRateTierAndAbove"];
    }

    private static string FormatNumber(decimal value) => DecimalInput.Format(value, CultureInfo.CurrentCulture, MaxDecimals);

    private string Lookup(string key) => Errors.TryGetValue(key, out var message) ? message : string.Empty;

    private void AddTierRow(string upTo, string price)
    {
        var row = new TariffTierRowViewModel { UpToText = upTo, PriceText = price };
        Relabel(row);
        row.RemoveCommand = new RelayCommand(() => RemoveTier(row));
        _tiers.Add(row);
        RefreshTierRows();
    }

    private void RemoveTier(TariffTierRowViewModel row)
    {
        if (_tiers.Count <= 2)
        {
            return;
        }

        _tiers.Remove(row);
        RefreshTierRows();
    }

    /// <summary>Marks the last row ("and above", no threshold) and lets rows be removed while at least two steps remain.</summary>
    private void RefreshTierRows()
    {
        for (var i = 0; i < _tiers.Count; i++)
        {
            _tiers[i].IsLast = i == _tiers.Count - 1;
            _tiers[i].CanRemove = _tiers.Count > 2;
            if (_tiers[i].IsLast)
            {
                _tiers[i].UpToText = string.Empty;
            }
        }

        TierRows = [.. _tiers];
    }

    /// <summary>Parses a price or threshold; a bad text, a huge number or more than six decimals adds an error under <paramref name="field"/>.</summary>
    private static decimal ParseNumber(string text, string field, List<ValidationError> errors)
    {
        if (!DecimalInput.TryParse(text, CultureInfo.CurrentCulture, out var value))
        {
            errors.Add(new(CommonErrorCodes.NumberInvalid, field));
            return 0m;
        }

        if (Math.Abs(value) > MaxNumber)
        {
            errors.Add(new(CommonErrorCodes.NumberTooLarge, field, [FormatNumber(MaxNumber)]));
            return 0m;
        }

        if (Math.Round(value, MaxDecimals) != value)
        {
            errors.Add(new(CommonErrorCodes.PriceTooManyDecimals, field));
            return 0m;
        }

        return value;
    }
}
