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
    private readonly HashSet<string> _expandedGroups = [];

    private Property _property = new();
    private Property? _pending;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedKindIndex))]
    private IReadOnlyList<EnumOption> _kinds = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedKindIndex))]
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

    /// <summary>True when the editor is shown on its own page (compact layout) instead of the right-hand pane.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFooter), nameof(ShowFormDelete))]
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
    private string _generalHeader = string.Empty;

    [ObservableProperty]
    private string _addParameterText = string.Empty;

    [ObservableProperty]
    private string _deleteWholeText = string.Empty;

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
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsNew))
            {
                OnPropertyChanged(nameof(ShowFormDelete));
            }
        };
        RefreshTexts();
    }

    /// <summary>Called when the session ended, after the editor navigated back if it is hosted in a page.</summary>
    public Func<EditOutcome, Task>? Finished { get; set; }

    public Guid PropertyId => _property.Id;

    public bool HasNoParameters => Groups.Count == 0;

    /// <summary>Save / Cancel / Delete footer of the side pane (medium and expanded layouts).</summary>
    public bool ShowFooter => !IsHostedInPage;

    /// <summary>"Delete property" at the end of the form on its own page (compact layout).</summary>
    public bool ShowFormDelete => IsHostedInPage && !IsNew;

    /// <summary>Index into <see cref="Kinds"/> for the picker; out-of-range values (the picker clearing itself) are ignored.</summary>
    public int SelectedKindIndex
    {
        get => SelectedKind is null ? -1 : Kinds.ToList().IndexOf(SelectedKind);
        set
        {
            if (value >= 0 && value < Kinds.Count)
            {
                SelectedKind = Kinds[value];
            }
        }
    }

    public string NameError => Lookup(Errors, nameof(Property.Name));

    public string NotesError => Lookup(Errors, nameof(Property.Notes));

    /// <summary>Errors about the parameters as a whole (not tied to a row).</summary>
    public string ParametersError => string.Join(
        "\n",
        Errors.Where(e => e.Key.StartsWith("Parameters.", StringComparison.Ordinal)).Select(e => e.Value));

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
            OnPropertyChanged(nameof(SelectedKindIndex));
            Address = _property.Address ?? string.Empty;
            Notes = _property.Notes ?? string.Empty;
            IsArchived = _property.IsArchived;

            _rows.Clear();
            foreach (var parameter in _property.Parameters.Where(p => !p.IsDeleted))
            {
                _rows.Add(CreateRow(parameter));
            }

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
    private async Task AddParameterAsync()
    {
        var editor = new ParameterEditorViewModel(_localizer, _clock, null, candidate => IsDuplicate(candidate, null));
        if (!await Dialogs.ShowEditorAsync(editor) || editor.Result is not { } result)
        {
            return;
        }

        _rows.Add(CreateRow(result));
        RebuildGroups();
        MarkDirty();
    }

    [RelayCommand]
    private async Task EditParameterAsync(ParameterRowViewModel row)
    {
        var current = new PropertyParameter
        {
            Kind = row.Kind,
            Name = row.Name,
            ValidFrom = row.ValidFrom,
            Value = row.Value,
            Note = row.Note,
        };
        var editor = new ParameterEditorViewModel(_localizer, _clock, current, candidate => IsDuplicate(candidate, row));
        if (!await Dialogs.ShowEditorAsync(editor) || editor.Result is not { } result)
        {
            return;
        }

        row.Kind = result.Kind;
        row.Name = result.Name;
        row.ValidFrom = result.ValidFrom;
        row.Value = result.Value;
        row.Note = result.Note;
        row.ErrorText = string.Empty;
        RebuildGroups();
        MarkDirty();
    }

    [RelayCommand]
    private async Task RemoveParameterAsync(ParameterRowViewModel row)
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

        _rows.Remove(row);
        RebuildGroups();
        MarkDirty();
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

    protected override string DeleteName() => _property.Name;

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

            NameLabel = _localizer["PropertyName"];
            KindLabel = _localizer["PropertyKindLabel"];
            AddressLabel = _localizer["PropertyAddress"];
            NotesLabel = _localizer["PropertyNotes"];
            ArchivedLabel = _localizer["PropertyArchived"];
            ParametersHeader = _localizer["ParametersHeader"];
            ParametersEmptyText = _localizer["ParametersEmpty"];
            GeneralHeader = _localizer["PropertyGeneral"];
            AddParameterText = _localizer["ParameterAdd"];
            DeleteWholeText = _localizer["PropertyDelete"];
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

    private void OnGroupToggled(ParameterGroupViewModel group)
    {
        if (group.IsExpanded)
        {
            _expandedGroups.Add(group.Key);
        }
        else
        {
            _expandedGroups.Remove(group.Key);
        }
    }

    private bool IsDuplicate(PropertyParameter candidate, ParameterRowViewModel? editing) =>
        _rows.Any(r => !ReferenceEquals(r, editing) && r.Kind == candidate.Kind && r.Name == candidate.Name && r.ValidFrom == candidate.ValidFrom);

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
                row.DeleteText = _localizer["Delete"];
            }

            var current = ordered.FirstOrDefault(r => r.ValidFrom <= today);
            var currentText = current is null
                ? _localizer["ParameterNoCurrent"]
                : _localizer.Get("ParameterCurrent", DecimalInput.Format(current.Value, CultureInfo.CurrentCulture, 4), current.ValidFrom);
            var title = group.Key.Kind == PropertyParameterKind.Custom
                ? group.Key.Name
                : EnumText.Get(_localizer, group.Key.Kind);
            var key = $"{(int)group.Key.Kind}:{group.Key.Name}";
            groups.Add(new ParameterGroupViewModel(key, title, currentText, _localizer.Get("ParameterHistory", ordered.Count), ordered)
            {
                IsExpanded = _expandedGroups.Contains(key),
                Toggled = OnGroupToggled,
            });
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
