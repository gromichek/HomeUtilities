using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Editor of one VAT catalog entry: name, code and the percentages over time. The code of the seeded entries is read-only
/// and they cannot be deleted; an entry used by tariff components cannot be deleted either.
/// </summary>
public sealed partial class VatRateEditViewModel : PaneEditorViewModel
{
    protected override HelpTopic? GuideTopic => HelpTopic.VatRates;

    private static readonly Regex VersionErrorField = new(@"^Versions\[(\d+)\]\.", RegexOptions.Compiled);

    private static readonly HashSet<string> DirtyFields = [nameof(RateName), nameof(Code), nameof(Notes)];

    private readonly IVatRateStore _store;
    private readonly ITariffStore _tariffStore;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;
    private readonly IDialogService _dialogs;
    private readonly List<VatVersionRowViewModel> _versionRows = [];

    private VatRate _rate = new();
    private VatRate? _pending;
    private List<VatVersionRowViewModel> _buildOrder = [];
    private HashSet<string> _takenCodes = [];

    [ObservableProperty]
    private string _rateName = string.Empty;

    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoVersions))]
    private IReadOnlyList<VatVersionRowViewModel> _versions = [];

    public VatRateEditViewModel(
        IVatRateStore store,
        ITariffStore tariffStore,
        IClock clock,
        ILocalizer localizer,
        IDialogService dialogs,
        INavigationService navigation,
        ISnackbarService toast)
        : base(localizer, dialogs, navigation, toast)
    {
        _store = store;
        _tariffStore = tariffStore;
        _clock = clock;
        _localizer = localizer;
        _dialogs = dialogs;
        RefreshTexts();
    }

    public override Guid EntityId => _rate.Id;

    /// <summary>The code of a seeded entry is fixed: other data and components refer to it.</summary>
    public bool IsCodeReadOnly => !IsNew && LatvianVatDefaults.IsDefaultCode(_rate.Code);

    public bool IsCodeEditable => !IsCodeReadOnly;

    /// <summary>Seeded entries are never deleted.</summary>
    public override bool CanShowDelete => !IsNew && !IsCodeReadOnly;

    public bool HasNoVersions => Versions.Count == 0;

    public string NameError => Lookup(nameof(VatRate.Name));

    public string CodeError => Lookup(nameof(VatRate.Code));

    public string NotesError => Lookup(nameof(VatRate.Notes));

    public string VersionsError => Join(Lookup(nameof(VatRate.Versions)));

    public string GeneralHeader => _localizer["VatGeneral"];

    public string NameLabel => _localizer["VatNameLabel"];

    public string CodeLabel => _localizer["VatCodeLabel"];

    public string CodeHint => _localizer["VatCodeHint"];

    public string NotesLabel => _localizer["VatNotesLabel"];

    public string VersionsHeader => _localizer["VatVersionsHeader"];

    public string VersionsEmptyText => _localizer["VatVersionsEmpty"];

    public string AddVersionText => _localizer["VatVersionAdd"];

    public override async Task LoadAsync(Guid? id)
    {
        var previous = SuppressDirty;
        SuppressDirty = true;
        try
        {
            var all = await _store.ListAsync();
            var existing = id is { } value ? all.FirstOrDefault(r => r.Id == value) : null;
            _rate = existing ?? new VatRate();
            _takenCodes = [.. all.Where(r => r.Id != _rate.Id).Select(r => r.Code)];
            IsNew = existing is null;
            RateName = _rate.Name;
            Code = _rate.Code;
            Notes = _rate.Notes ?? string.Empty;

            _versionRows.Clear();
            foreach (var version in _rate.Versions.Where(v => !v.IsDeleted))
            {
                _versionRows.Add(CreateRow(version));
            }

            RebuildVersions();
            ClearErrors();
            NotifyCodeState();
            NotifyDeleteVisibilityChanged();
            Title = TitleText();
            IsDirty = false;
        }
        finally
        {
            SuppressDirty = previous;
        }
    }

    [RelayCommand]
    private Task AddVersionAsync() => RunSafeAsync(async () =>
    {
        var editor = new VatVersionEditorViewModel(_localizer, _clock, null, candidate => IsDuplicate(candidate, null));
        if (!await Dialogs.ShowEditorAsync(editor) || editor.Result is not { } result)
        {
            return;
        }

        result.VatRateId = _rate.Id;
        _versionRows.Add(CreateRow(result));
        RebuildVersions();
        MarkDirty();
    });

    [RelayCommand]
    private Task EditVersionAsync(VatVersionRowViewModel row) => RunSafeAsync(async () =>
    {
        var editor = new VatVersionEditorViewModel(_localizer, _clock, row.Version, candidate => IsDuplicate(candidate, row));
        if (!await Dialogs.ShowEditorAsync(editor) || editor.Result is not { } result)
        {
            return;
        }

        result.VatRateId = _rate.Id;
        row.Version = result;
        row.ErrorText = string.Empty;
        RebuildVersions();
        MarkDirty();
    });

    [RelayCommand]
    private Task RemoveVersionAsync(VatVersionRowViewModel row) => RunSafeAsync(async () =>
    {
        var confirmed = await Dialogs.ConfirmAsync(
            _localizer["Dialog_DeleteRow_Title"],
            _localizer.Get("Dialog_DeleteRow_Message", row.DisplayText),
            _localizer["Delete"],
            _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        _versionRows.Remove(row);
        RebuildVersions();
        MarkDirty();
    });

    protected override IReadOnlyList<ValidationError> Validate()
    {
        _pending = BuildRate();
        var errors = new List<ValidationError>(VatRateValidator.Validate(_pending).Errors);
        if (_versionRows.Count == 0)
        {
            errors.Add(new(CommonErrorCodes.VatVersionRequired, nameof(VatRate.Versions)));
        }

        if (_takenCodes.Contains(_pending.Code))
        {
            errors.Add(new(CommonErrorCodes.VatCodeDuplicate, nameof(VatRate.Code)));
        }

        return errors;
    }

    protected override async Task SaveCoreAsync()
    {
        var pending = _pending ?? BuildRate();
        await _store.SaveAsync(pending);
        _rate = pending;
        IsNew = false;
        Title = TitleText();
        NotifyCodeState();
        NotifyDeleteVisibilityChanged();
    }

    protected override async Task<bool> CanDeleteAsync()
    {
        var used = (await _tariffStore.ListAsync()).Count(c => !c.IsDeleted && c.VatCode == _rate.Code);
        if (used == 0)
        {
            return true;
        }

        await _dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer.Get("VatRateInUse", _rate.Name, used));
        return false;
    }

    protected override Task DeleteCoreAsync() => _store.DeleteAsync(_rate.Id);

    protected override string DeleteName() => _rate.Name;

    protected override void OnErrorsChanged()
    {
        foreach (var name in new[] { nameof(NameError), nameof(CodeError), nameof(NotesError), nameof(VersionsError) })
        {
            OnPropertyChanged(name);
        }

        foreach (var row in _versionRows)
        {
            row.ErrorText = string.Empty;
        }

        foreach (var (field, message) in Errors)
        {
            var match = VersionErrorField.Match(field);
            if (match.Success
                && int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) is var index
                && index < _buildOrder.Count)
            {
                var row = _buildOrder[index];
                row.ErrorText = row.ErrorText.Length == 0 ? message : row.ErrorText + "\n" + message;
            }
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is { } name && DirtyFields.Contains(name))
        {
            MarkDirty();
        }
    }

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        DeleteWholeText = _localizer["VatRateDelete"];
        Title = TitleText();
        RebuildVersions();
        foreach (var name in new[]
        {
            nameof(GeneralHeader), nameof(NameLabel), nameof(CodeLabel), nameof(CodeHint), nameof(NotesLabel), nameof(VersionsHeader),
            nameof(VersionsEmptyText), nameof(AddVersionText),
        })
        {
            OnPropertyChanged(name);
        }
    }

    private string Lookup(string key) => Errors.TryGetValue(key, out var message) ? message : string.Empty;

    private static string Join(params string[] messages) => string.Join("\n", messages.Where(m => m.Length > 0));

    private string TitleText() => IsNew ? _localizer["VatRateNew"] : _rate.Name;

    private void NotifyCodeState()
    {
        OnPropertyChanged(nameof(IsCodeReadOnly));
        OnPropertyChanged(nameof(IsCodeEditable));
    }

    private VatVersionRowViewModel CreateRow(VatRateVersion version)
    {
        var row = new VatVersionRowViewModel(version);
        row.EditCommand = new RelayCommand(() => EditVersionCommand.Execute(row));
        row.RemoveCommand = new RelayCommand(() => RemoveVersionCommand.Execute(row));
        return row;
    }

    private bool IsDuplicate(VatRateVersion candidate, VatVersionRowViewModel? editing) =>
        _versionRows.Any(r => !ReferenceEquals(r, editing) && r.Version.ValidFrom == candidate.ValidFrom);

    private void RebuildVersions()
    {
        foreach (var row in _versionRows)
        {
            row.DisplayText = _localizer.Get(
                "VatVersionRow", row.Version.ValidFrom, DecimalInput.Format(row.Version.Percent, CultureInfo.CurrentCulture, 4));
            row.DeleteText = _localizer["Delete"];
        }

        Versions = [.. _versionRows.OrderByDescending(r => r.Version.ValidFrom)];
    }

    private VatRate BuildRate()
    {
        var rate = new VatRate
        {
            Id = _rate.Id,
            CreatedAt = _rate.CreatedAt,
            UpdatedAt = _rate.UpdatedAt,
            Code = IsCodeReadOnly ? _rate.Code : Code.Trim().ToUpperInvariant(),
            Name = RateName.Trim(),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
        };
        _buildOrder = [.. _versionRows.OrderBy(r => r.Version.ValidFrom)];
        foreach (var row in _buildOrder)
        {
            row.Version.VatRateId = rate.Id;
            rate.Versions.Add(row.Version);
        }

        return rate;
    }
}
