using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>The dates a meter belongs to a contract; the result of <see cref="MeterLinkEditorViewModel"/>.</summary>
/// <param name="ValidFrom">First day of the link.</param>
/// <param name="ValidTo">Last day of the link; null if open-ended.</param>
public sealed record MeterLinkDates(DateOnly ValidFrom, DateOnly? ValidTo);

/// <summary>Dialog that edits the dates of one link between a meter and a contract.</summary>
public sealed partial class MeterLinkEditorViewModel : EditorDialogViewModel
{
    private readonly ILocalizer _localizer;
    private readonly ContractMeter _link;
    private readonly DateOnly _meterInstalledOn;
    private readonly Contract _contract;
    private readonly IReadOnlyList<ContractMeter> _otherLinks;

    [ObservableProperty]
    private DateOnly _validFrom;

    [ObservableProperty]
    private bool _hasValidTo;

    [ObservableProperty]
    private DateOnly _validTo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidFromError), nameof(ValidToError))]
    private IReadOnlyDictionary<string, string> _errors = new Dictionary<string, string>();

    [ObservableProperty]
    private string _warningText = string.Empty;

    [ObservableProperty]
    private string _validFromLabel = string.Empty;

    [ObservableProperty]
    private string _hasValidToLabel = string.Empty;

    [ObservableProperty]
    private string _validToLabel = string.Empty;

    /// <param name="localizer">Localizer.</param>
    /// <param name="link">The link to edit; it is not changed (read <see cref="Result"/> after Save).</param>
    /// <param name="meterInstalledOn">Installation date of the meter.</param>
    /// <param name="contract">The contract of the link.</param>
    /// <param name="otherLinks">The other links of the same meter and contract.</param>
    public MeterLinkEditorViewModel(
        ILocalizer localizer, ContractMeter link, DateOnly meterInstalledOn, Contract contract, IEnumerable<ContractMeter> otherLinks)
        : base(localizer)
    {
        _localizer = localizer;
        _link = link;
        _meterInstalledOn = meterInstalledOn;
        _contract = contract;
        _otherLinks = [.. otherLinks.Where(o => o.Id != link.Id)];
        ContextText = contract.Title;
        _validFrom = link.ValidFrom;
        _hasValidTo = link.ValidTo is not null;
        _validTo = link.ValidTo ?? link.ValidFrom;
        UpdateWarning();
        RefreshTexts();
    }

    /// <summary>The contract the link belongs to, shown under the title.</summary>
    public string ContextText { get; }

    public string ValidFromError => Lookup(nameof(ContractMeter.ValidFrom));

    public string ValidToError => Lookup(nameof(ContractMeter.ValidTo));

    /// <summary>True while a warning is shown (the link starts before the contract).</summary>
    public bool HasWarning => WarningText.Length > 0;

    /// <summary>The validated dates; meaningful only after <see cref="EditorDialogViewModel.Saved"/>.</summary>
    public MeterLinkDates? Result { get; private set; }

    partial void OnValidFromChanged(DateOnly value) => UpdateWarning();

    partial void OnWarningTextChanged(string value) => OnPropertyChanged(nameof(HasWarning));

    protected override bool TryApply()
    {
        var candidate = Candidate();
        var check = MeterLinkRules.Check(candidate, _meterInstalledOn, _contract.ValidFrom, _otherLinks);
        Errors = check.Errors
            .GroupBy(e => e.Field ?? string.Empty)
            .ToDictionary(g => g.Key, g => string.Join("\n", g.Select(e => ValidationText.For(_localizer, e))));
        if (!check.IsValid)
        {
            return false;
        }

        Result = new MeterLinkDates(candidate.ValidFrom, candidate.ValidTo);
        return true;
    }

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        Title = _localizer["MeterLinkEdit"];
        ValidFromLabel = _localizer["MeterLinkValidFrom"];
        HasValidToLabel = _localizer["MeterLinkHasValidTo"];
        ValidToLabel = _localizer["MeterLinkValidTo"];
        UpdateWarning();
    }

    private ContractMeter Candidate() => new()
    {
        Id = _link.Id,
        ContractId = _link.ContractId,
        MeterId = _link.MeterId,
        ValidFrom = ValidFrom,
        ValidTo = HasValidTo ? ValidTo : null,
    };

    private void UpdateWarning() =>
        WarningText = ValidFrom < _contract.ValidFrom ? _localizer.Get("MeterLinkBeforeContract", _contract.ValidFrom) : string.Empty;

    private string Lookup(string key) => Errors.TryGetValue(key, out var message) ? message : string.Empty;
}
