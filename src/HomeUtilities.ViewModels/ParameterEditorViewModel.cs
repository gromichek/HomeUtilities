using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>Dialog that adds or edits one value of a time-versioned property parameter.</summary>
public sealed partial class ParameterEditorViewModel : EditorDialogViewModel
{
    private readonly ILocalizer _localizer;
    private readonly Func<PropertyParameter, bool> _isDuplicate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedKindIndex))]
    private IReadOnlyList<EnumOption> _kinds = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustom), nameof(SelectedKindIndex))]
    private EnumOption? _selectedKind;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _valueText = string.Empty;

    [ObservableProperty]
    private DateOnly _validFrom;

    [ObservableProperty]
    private string _note = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueError), nameof(NameError), nameof(ValidFromError), nameof(NoteError))]
    private IReadOnlyDictionary<string, string> _errors = new Dictionary<string, string>();

    [ObservableProperty]
    private string _kindLabel = string.Empty;

    [ObservableProperty]
    private string _nameLabel = string.Empty;

    [ObservableProperty]
    private string _valueLabel = string.Empty;

    [ObservableProperty]
    private string _validFromLabel = string.Empty;

    [ObservableProperty]
    private string _noteLabel = string.Empty;

    /// <param name="existing">The value to edit, or null to add a new one.</param>
    /// <param name="isDuplicate">Tells whether another row already has the candidate's kind, name and valid-from date.</param>
    public ParameterEditorViewModel(
        ILocalizer localizer, IClock clock, PropertyParameter? existing, Func<PropertyParameter, bool> isDuplicate)
        : base(localizer)
    {
        _localizer = localizer;
        _isDuplicate = isDuplicate;
        IsNew = existing is null;

        var kind = existing?.Kind ?? PropertyParameterKind.TotalArea;
        Kinds = EnumText.Options<PropertyParameterKind>(localizer);
        SelectedKind = Kinds.First(k => k.Value.Equals(kind));
        Name = existing?.Name ?? string.Empty;
        ValueText = existing is null ? string.Empty : DecimalInput.Format(existing.Value, CultureInfo.CurrentCulture, 4);
        ValidFrom = existing?.ValidFrom ?? clock.Today;
        Note = existing?.Note ?? string.Empty;
        RefreshTexts();
    }

    public bool IsNew { get; }

    public bool IsCustom => SelectedKind?.Value is PropertyParameterKind.Custom;

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

    public string ValueError => Lookup(nameof(PropertyParameter.Value));

    public string NameError => Lookup(nameof(PropertyParameter.Name));

    public string ValidFromError => Lookup(nameof(PropertyParameter.ValidFrom));

    public string NoteError => Lookup(nameof(PropertyParameter.Note));

    /// <summary>The validated value; meaningful only after <see cref="EditorDialogViewModel.Saved"/>.</summary>
    public PropertyParameter? Result { get; private set; }

    protected override bool TryApply()
    {
        var errors = new List<ValidationError>();
        var kind = SelectedKind?.As<PropertyParameterKind>() ?? PropertyParameterKind.TotalArea;
        if (!DecimalInput.TryParse(ValueText, CultureInfo.CurrentCulture, out var value))
        {
            errors.Add(new(CommonErrorCodes.NumberInvalid, nameof(PropertyParameter.Value)));
        }

        var candidate = new PropertyParameter
        {
            Kind = kind,
            Name = kind == PropertyParameterKind.Custom ? Name.Trim() : null,
            ValidFrom = ValidFrom,
            Value = value,
            Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
        };
        errors.AddRange(PropertyParameterValidator.Validate(candidate).Errors);
        if (_isDuplicate(candidate))
        {
            errors.Add(new(ErrorCodes.DuplicateValidFrom, nameof(PropertyParameter.ValidFrom), [candidate.ValidFrom]));
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
        var kind = SelectedKind?.Value;
        Kinds = EnumText.Options<PropertyParameterKind>(_localizer);
        if (kind is not null)
        {
            SelectedKind = Kinds.First(k => k.Value.Equals(kind));
        }

        Title = _localizer[IsNew ? "ParameterNew" : "ParameterEdit"];
        KindLabel = _localizer["ParameterKind"];
        NameLabel = _localizer["ParameterName"];
        ValueLabel = _localizer["ParameterValue"];
        ValidFromLabel = _localizer["ParameterValidFrom"];
        NoteLabel = _localizer["ParameterNote"];
    }

    private string Lookup(string key) => Errors.TryGetValue(key, out var message) ? message : string.Empty;
}
