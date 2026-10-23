using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Editor of one payment category. A built-in category can be renamed (empty name = the default localized name),
/// recoloured and archived, but not deleted.
/// </summary>
public sealed partial class CategoryEditViewModel : PaneEditorViewModel
{
    private readonly ICategoryStore _store;
    private readonly ILocalizer _localizer;

    private Category _category = new();
    private Category? _pending;

    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>The colour as typed: <c>#RRGGBB</c>.</summary>
    [ObservableProperty]
    private string _color = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private bool _isArchived;

    [ObservableProperty]
    private IReadOnlyList<ColorSwatchViewModel> _swatches = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNameHint))]
    private string _nameHint = string.Empty;

    [ObservableProperty]
    private string _generalHeader = string.Empty;

    [ObservableProperty]
    private string _nameLabel = string.Empty;

    [ObservableProperty]
    private string _colorLabel = string.Empty;

    [ObservableProperty]
    private string _colorHexLabel = string.Empty;

    [ObservableProperty]
    private string _notesLabel = string.Empty;

    [ObservableProperty]
    private string _archivedLabel = string.Empty;

    public CategoryEditViewModel(
        ICategoryStore store,
        ILocalizer localizer,
        IDialogService dialogs,
        INavigationService navigation,
        IToastService toast)
        : base(localizer, dialogs, navigation, toast)
    {
        _store = store;
        _localizer = localizer;
        RefreshTexts();
    }

    public override Guid EntityId => _category.Id;

    public bool IsBuiltIn => _category.IsBuiltIn;

    /// <summary>Built-in categories are never deleted.</summary>
    public override bool CanShowDelete => !IsNew && !IsBuiltIn;

    /// <summary>"Default: Water" under the name of a built-in category.</summary>
    public bool HasNameHint => NameHint.Length > 0;

    public string NameError => Lookup(nameof(Category.Name));

    public string ColorError => Lookup(nameof(Category.Color));

    public string NotesError => Lookup(nameof(Category.Notes));

    public override async Task LoadAsync(Guid? id)
    {
        var previous = SuppressDirty;
        SuppressDirty = true;
        try
        {
            var all = await _store.ListAsync();
            var existing = id is { } value ? all.FirstOrDefault(c => c.Id == value) : null;
            _category = existing ?? new Category { Color = CategoryNames.NextColor(all) };
            IsNew = existing is null;
            Name = _category.Name ?? string.Empty;
            Color = _category.Color;
            Notes = _category.Notes ?? string.Empty;
            IsArchived = _category.IsArchived;

            ClearErrors();
            RefreshNameHint();
            RebuildSwatches();
            NotifyDeleteVisibilityChanged();
            Title = TitleText();
            IsDirty = false;
        }
        finally
        {
            SuppressDirty = previous;
        }
    }

    protected override IReadOnlyList<ValidationError> Validate()
    {
        _pending = BuildCategory();
        return CategoryValidator.Validate(_pending).Errors;
    }

    protected override async Task SaveCoreAsync()
    {
        var pending = _pending ?? BuildCategory();
        if (IsNew)
        {
            var all = await _store.ListAsync();
            pending.SortOrder = all.Select(c => c.SortOrder).DefaultIfEmpty(-1).Max() + 1;
        }

        await _store.SaveAsync(pending);
        _category = pending;
        IsNew = false;
        Title = TitleText();
        NotifyDeleteVisibilityChanged();
    }

    protected override Task DeleteCoreAsync() =>
        IsBuiltIn
            ? throw new InvalidOperationException("A built-in category cannot be deleted.")
            : _store.DeleteAsync(_category.Id);

    protected override string DeleteName() => CategoryNames.DisplayName(_category, _localizer);

    protected override void OnErrorsChanged()
    {
        OnPropertyChanged(nameof(NameError));
        OnPropertyChanged(nameof(ColorError));
        OnPropertyChanged(nameof(NotesError));
    }

    partial void OnNameChanged(string value) => MarkDirty();

    partial void OnColorChanged(string value)
    {
        MarkDirty();
        UpdateSwatchSelection();
    }

    partial void OnNotesChanged(string value) => MarkDirty();

    partial void OnIsArchivedChanged(bool value) => MarkDirty();

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        GeneralHeader = _localizer["CategoryGeneral"];
        NameLabel = _localizer["CategoryName"];
        ColorLabel = _localizer["CategoryColor"];
        ColorHexLabel = _localizer["CategoryColorHex"];
        NotesLabel = _localizer["CategoryNotes"];
        ArchivedLabel = _localizer["CategoryArchived"];
        DeleteWholeText = _localizer["CategoryDelete"];
        RefreshNameHint();
        Title = TitleText();
    }

    private string Lookup(string key) => Errors.TryGetValue(key, out var message) ? message : string.Empty;

    private string TitleText() => IsNew ? _localizer["CategoryNew"] : CategoryNames.DisplayName(_category, _localizer);

    private void RefreshNameHint() =>
        NameHint = _category.SystemKey is { } kind
            ? _localizer.Get("CategoryNameDefaultHint", CategoryNames.DefaultName(kind, _localizer))
            : string.Empty;

    private void RebuildSwatches()
    {
        Swatches = [.. CategoryNames.Palette.Select(c => new ColorSwatchViewModel(c, picked => Color = picked))];
        UpdateSwatchSelection();
    }

    private void UpdateSwatchSelection()
    {
        foreach (var swatch in Swatches)
        {
            swatch.IsSelected = string.Equals(swatch.Color, Color.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private Category BuildCategory() => new()
    {
        Id = _category.Id,
        CreatedAt = _category.CreatedAt,
        UpdatedAt = _category.UpdatedAt,
        SystemKey = _category.SystemKey,
        Icon = _category.Icon,
        SortOrder = _category.SortOrder,
        Name = string.IsNullOrWhiteSpace(Name) ? null : Name.Trim(),
        Color = Color.Trim().ToUpperInvariant(),
        Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
        IsArchived = IsArchived,
    };
}
