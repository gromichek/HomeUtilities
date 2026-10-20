using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>Editor of one property with its time-versioned parameters.</summary>
public sealed partial class PropertyEditViewModel : EditableViewModel
{
    private static readonly Regex RowErrorField = new(@"^Parameters\[(\d+)\]\.", RegexOptions.Compiled);

    private readonly IPropertyStore _store;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;
    private readonly INavigationService _navigation;
    private readonly List<ParameterRowViewModel> _rows = [];

    private Property _property = new();
    private Property? _pending;
    private ParameterRowViewModel? _editingRow;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<EnumOption> _kinds = [];

    [ObservableProperty]
    private EnumOption? _selectedKind;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private bool _isArchived;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoParameters))]
    private IReadOnlyList<ParameterGroupViewModel> _groups = [];

    [ObservableProperty]
    private bool _isParameterFormVisible;

    [ObservableProperty]
    private IReadOnlyList<EnumOption> _parameterKinds = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormCustom))]
    private EnumOption? _formKind;

    [ObservableProperty]
    private string _formName = string.Empty;

    [ObservableProperty]
    private string _formValueText = string.Empty;

    [ObservableProperty]
    private DateOnly _formValidFrom;

    [ObservableProperty]
    private string _formNote = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormValueError), nameof(FormNameError), nameof(FormValidFromError), nameof(FormNoteError))]
    private IReadOnlyDictionary<string, string> _formErrors = new Dictionary<string, string>();

    /// <summary>True when the editor is shown on its own page (compact layout) instead of the right-hand pane.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInlineActions))]
    private bool _isHostedInPage;

    [ObservableProperty]
    private string _nameLabel = string.Empty;

    [ObservableProperty]
    private string _kindLabel = string.Empty;

    [ObservableProperty]
    private string _addressLabel = string.Empty;

    [ObservableProperty]
    private string _notesLabel = string.Empty;

    [ObservableProperty]
    private string _archivedLabel = string.Empty;

    [ObservableProperty]
    private string _parametersHeader = string.Empty;

    [ObservableProperty]
    private string _parametersEmptyText = string.Empty;

    [ObservableProperty]
    private string _addParameterText = string.Empty;

    [ObservableProperty]
    private string _formKindLabel = string.Empty;

    [ObservableProperty]
    private string _formNameLabel = string.Empty;

    [ObservableProperty]
    private string _formValueLabel = string.Empty;

    [ObservableProperty]
    private string _formValidFromLabel = string.Empty;

    [ObservableProperty]
    private string _formNoteLabel = string.Empty;

    [ObservableProperty]
    private string _applyText = string.Empty;

    public PropertyEditViewModel(
        IPropertyStore store,
        IClock clock,
        ILocalizer localizer,
        IDialogService dialogs,
        INavigationService navigation)
        : base(localizer, dialogs)
    {
        _store = store;
        _clock = clock;
        _localizer = localizer;
        _navigation = navigation;
        _formValidFrom = clock.Today;
        RefreshTexts();
    }

    /// <summary>Called when the session ended, after the editor navigated back if it is hosted in a page.</summary>
    public Func<EditOutcome, Task>? Finished { get; set; }

    public Guid PropertyId => _property.Id;

    public bool IsFormCustom => FormKind?.Value is PropertyParameterKind.Custom;

    public bool HasNoParameters => Groups.Count == 0;

    public bool ShowInlineActions => !IsHostedInPage;

    public string NameError => Lookup(Errors, nameof(Property.Name));

    public string NotesError => Lookup(Errors, nameof(Property.Notes));

    /// <summary>Errors about the parameters as a whole (not tied to a row).</summary>
    public string ParametersError => string.Join(
        "\n",
        Errors.Where(e => e.Key.StartsWith("Parameters.", StringComparison.Ordinal)).Select(e => e.Value));

    public string FormValueError => Lookup(FormErrors, nameof(PropertyParameter.Value));

    public string FormNameError => Lookup(FormErrors, nameof(PropertyParameter.Name));

    public string FormValidFromError => Lookup(FormErrors, nameof(PropertyParameter.ValidFrom));

    public string FormNoteError => Lookup(FormErrors, nameof(PropertyParameter.Note));

    /// <summary>The rows of all parameters in entry order; indexes match <c>Parameters[i]</c> of validation errors.</summary>
    public IReadOnlyList<ParameterRowViewModel> Rows => _rows;

    /// <summary>Loads an existing property, or prepares a new one when <paramref name="id"/> is null.</summary>
    public async Task LoadAsync(Guid? id)
    {
        var previous = SuppressDirty;
        SuppressDirty = true;
        try
        {
            var existing = id is { } value ? await _store.GetAsync(value) : null;
            _property = existing ?? new Property();
            IsNew = existing is null;
            Name = _property.Name;
            SelectedKind = Kinds.First(k => k.Value.Equals(_property.Kind));
            Address = _property.Address ?? string.Empty;
            Notes = _property.Notes ?? string.Empty;
            IsArchived = _property.IsArchived;

            _rows.Clear();
            foreach (var parameter in _property.Parameters.Where(p => !p.IsDeleted))
            {
                _rows.Add(CreateRow(parameter));
            }

            IsParameterFormVisible = false;
            ClearErrors();
            RebuildGroups();
            Title = TitleText();
            IsDirty = false;
        }
        finally
        {
            SuppressDirty = previous;
        }
    }

    [RelayCommand]
    private void AddParameter()
    {
        _editingRow = null;
        OpenForm(PropertyParameterKind.TotalArea, string.Empty, string.Empty, _clock.Today, string.Empty);
    }

    [RelayCommand]
    private void EditParameter(ParameterRowViewModel row)
    {
        _editingRow = row;
        OpenForm(
            row.Kind,
            row.Name ?? string.Empty,
            DecimalInput.Format(row.Value, CultureInfo.CurrentCulture, 4),
            row.ValidFrom,
            row.Note ?? string.Empty);
    }

    [RelayCommand]
    private async Task RemoveParameterAsync(ParameterRowViewModel row)
    {
        var confirmed = await Dialogs.ConfirmAsync(
            _localizer["DeleteConfirmTitle"],
            _localizer.Get("ParameterRemoveMessage", row.DisplayText),
            _localizer["Remove"],
            _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        _rows.Remove(row);
        if (ReferenceEquals(_editingRow, row))
        {
            IsParameterFormVisible = false;
            _editingRow = null;
        }

        RebuildGroups();
        MarkDirty();
    }

    [RelayCommand]
    private void ApplyParameter()
    {
        var errors = new List<ValidationError>();
        var kind = FormKind?.As<PropertyParameterKind>() ?? PropertyParameterKind.TotalArea;
        var parsed = DecimalInput.TryParse(FormValueText, CultureInfo.CurrentCulture, out var value);
        if (!parsed)
        {
            errors.Add(new(CommonErrorCodes.NumberInvalid, nameof(PropertyParameter.Value)));
        }

        var candidate = new PropertyParameter
        {
            Kind = kind,
            Name = kind == PropertyParameterKind.Custom ? FormName.Trim() : null,
            ValidFrom = FormValidFrom,
            Value = value,
            Note = string.IsNullOrWhiteSpace(FormNote) ? null : FormNote.Trim(),
        };
        errors.AddRange(PropertyParameterValidator.Validate(candidate).Errors);

        if (_rows.Any(r => !ReferenceEquals(r, _editingRow) && r.Kind == kind && r.Name == candidate.Name && r.ValidFrom == candidate.ValidFrom))
        {
            errors.Add(new(ErrorCodes.DuplicateValidFrom, nameof(PropertyParameter.ValidFrom), [candidate.ValidFrom]));
        }

        FormErrors = errors
            .GroupBy(e => e.Field ?? string.Empty)
            .ToDictionary(g => g.Key, g => string.Join("\n", g.Select(e => ValidationText.For(_localizer, e))));
        if (errors.Count > 0)
        {
            return;
        }

        var row = _editingRow;
        if (row is null)
        {
            row = CreateRow(candidate);
            _rows.Add(row);
        }
        else
        {
            row.Kind = candidate.Kind;
            row.Name = candidate.Name;
            row.ValidFrom = candidate.ValidFrom;
            row.Value = candidate.Value;
            row.Note = candidate.Note;
            row.ErrorText = string.Empty;
        }

        _editingRow = null;
        IsParameterFormVisible = false;
        RebuildGroups();
        MarkDirty();
    }

    [RelayCommand]
    private void CancelParameter()
    {
        _editingRow = null;
        IsParameterFormVisible = false;
        FormErrors = new Dictionary<string, string>();
    }

    protected override IReadOnlyList<ValidationError> Validate()
    {
        _pending = BuildProperty();
        return PropertyValidator.Validate(_pending).Errors;
    }

    protected override async Task SaveCoreAsync()
    {
        var pending = _pending ?? BuildProperty();
        if (IsNew)
        {
            var all = await _store.ListAsync(true);
            pending.SortOrder = all.Select(p => p.SortOrder).DefaultIfEmpty(-1).Max() + 1;
        }

        await _store.SaveAsync(pending);
        _property = pending;
        IsNew = false;
        Title = TitleText();
    }

    protected override Task DeleteCoreAsync() => _store.DeleteAsync(_property.Id);

    protected override string DeleteMessage() => _localizer.Get("PropertyDeleteMessage", _property.Name);

    protected override async Task OnFinishedAsync(EditOutcome outcome)
    {
        if (IsHostedInPage)
        {
            await _navigation.GoBackAsync();
        }

        if (Finished is { } finished)
        {
            await finished(outcome);
        }
    }

    protected override void OnErrorsChanged()
    {
        OnPropertyChanged(nameof(NameError));
        OnPropertyChanged(nameof(NotesError));
        OnPropertyChanged(nameof(ParametersError));

        foreach (var row in _rows)
        {
            row.ErrorText = string.Empty;
        }

        foreach (var (field, message) in Errors)
        {
            var match = RowErrorField.Match(field);
            if (match.Success && int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) is var index && index < _rows.Count)
            {
                var row = _rows[index];
                row.ErrorText = row.ErrorText.Length == 0 ? message : row.ErrorText + "\n" + message;
            }
        }
    }

    partial void OnNameChanged(string value) => MarkDirty();

    partial void OnSelectedKindChanged(EnumOption? value) => MarkDirty();

    partial void OnAddressChanged(string value) => MarkDirty();

    partial void OnNotesChanged(string value) => MarkDirty();

    partial void OnIsArchivedChanged(bool value) => MarkDirty();

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        var previous = SuppressDirty;
        SuppressDirty = true;
        try
        {
            var kind = SelectedKind?.Value ?? PropertyKind.House;
            Kinds = EnumText.Options<PropertyKind>(_localizer);
            SelectedKind = Kinds.First(k => k.Value.Equals(kind));

            var formKind = FormKind?.Value ?? PropertyParameterKind.TotalArea;
            ParameterKinds = EnumText.Options<PropertyParameterKind>(_localizer);
            FormKind = ParameterKinds.First(k => k.Value.Equals(formKind));

            NameLabel = _localizer["PropertyName"];
            KindLabel = _localizer["PropertyKindLabel"];
            AddressLabel = _localizer["PropertyAddress"];
            NotesLabel = _localizer["PropertyNotes"];
            ArchivedLabel = _localizer["PropertyArchived"];
            ParametersHeader = _localizer["ParametersHeader"];
            ParametersEmptyText = _localizer["ParametersEmpty"];
            AddParameterText = _localizer["ParameterAdd"];
            FormKindLabel = _localizer["ParameterKind"];
            FormNameLabel = _localizer["ParameterName"];
            FormValueLabel = _localizer["ParameterValue"];
            FormValidFromLabel = _localizer["ParameterValidFrom"];
            FormNoteLabel = _localizer["ParameterNote"];
            ApplyText = _localizer["Apply"];
            Title = TitleText();
            RebuildGroups();
        }
        finally
        {
            SuppressDirty = previous;
        }
    }

    private static string Lookup(IReadOnlyDictionary<string, string> errors, string key) =>
        errors.TryGetValue(key, out var message) ? message : string.Empty;

    private string TitleText() => IsNew ? _localizer["PropertyNew"] : _property.Name;

    private void OpenForm(PropertyParameterKind kind, string name, string valueText, DateOnly validFrom, string note)
    {
        FormKind = ParameterKinds.First(k => k.Value.Equals(kind));
        FormName = name;
        FormValueText = valueText;
        FormValidFrom = validFrom;
        FormNote = note;
        FormErrors = new Dictionary<string, string>();
        IsParameterFormVisible = true;
    }

    private ParameterRowViewModel CreateRow(PropertyParameter parameter)
    {
        var row = new ParameterRowViewModel
        {
            Id = parameter.Id,
            CreatedAt = parameter.CreatedAt,
            Kind = parameter.Kind,
            Name = parameter.Name,
            ValidFrom = parameter.ValidFrom,
            Value = parameter.Value,
            Note = parameter.Note,
        };
        row.EditCommand = new RelayCommand(() => EditParameterCommand.Execute(row));
        row.RemoveCommand = new RelayCommand(() => RemoveParameterCommand.Execute(row));
        return row;
    }

    private void RebuildGroups()
    {
        var today = _clock.Today;
        var groups = new List<ParameterGroupViewModel>();
        var grouped = _rows
            .GroupBy(r => (r.Kind, Name: r.Kind == PropertyParameterKind.Custom ? r.Name ?? string.Empty : string.Empty))
            .OrderBy(g => g.Key.Kind)
            .ThenBy(g => g.Key.Name, StringComparer.CurrentCultureIgnoreCase);

        foreach (var group in grouped)
        {
            var ordered = group.OrderByDescending(r => r.ValidFrom).ToList();
            foreach (var row in ordered)
            {
                row.DisplayText = _localizer.Get(
                    "ParameterRow", DecimalInput.Format(row.Value, CultureInfo.CurrentCulture, 4), row.ValidFrom);
                row.EditText = _localizer["Edit"];
                row.RemoveText = _localizer["Remove"];
            }

            var current = ordered.FirstOrDefault(r => r.ValidFrom <= today);
            var currentText = current is null
                ? _localizer["ParameterNoCurrent"]
                : _localizer.Get("ParameterCurrent", DecimalInput.Format(current.Value, CultureInfo.CurrentCulture, 4));
            var title = group.Key.Kind == PropertyParameterKind.Custom
                ? group.Key.Name
                : EnumText.Get(_localizer, group.Key.Kind);
            groups.Add(new ParameterGroupViewModel(title, currentText, ordered));
        }

        Groups = groups;
    }

    private Property BuildProperty()
    {
        var property = new Property
        {
            Id = _property.Id,
            CreatedAt = _property.CreatedAt,
            UpdatedAt = _property.UpdatedAt,
            SortOrder = _property.SortOrder,
            Name = Name.Trim(),
            Kind = SelectedKind?.As<PropertyKind>() ?? PropertyKind.House,
            Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            IsArchived = IsArchived,
        };

        foreach (var row in _rows)
        {
            property.Parameters.Add(new PropertyParameter
            {
                Id = row.Id,
                CreatedAt = row.CreatedAt,
                PropertyId = property.Id,
                Kind = row.Kind,
                Name = row.Kind == PropertyParameterKind.Custom ? row.Name : null,
                ValidFrom = row.ValidFrom,
                Value = row.Value,
                Note = row.Note,
            });
        }

        return property;
    }
}
