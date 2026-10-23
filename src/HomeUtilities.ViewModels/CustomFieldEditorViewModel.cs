using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>Dialog that adds or edits one custom field definition of a contract.</summary>
public sealed partial class CustomFieldEditorViewModel : EditorDialogViewModel
{
    private readonly ILocalizer _localizer;
    private readonly Func<CustomFieldDefinition, bool> _isDuplicate;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedKindIndex))]
    private IReadOnlyList<EnumOption> _kinds = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedKindIndex))]
    private EnumOption? _selectedKind;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTargetIndex))]
    private IReadOnlyList<EnumOption> _targets = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTargetIndex))]
    private EnumOption? _selectedTarget;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NameError))]
    private IReadOnlyDictionary<string, string> _errors = new Dictionary<string, string>();

    [ObservableProperty]
    private string _nameLabel = string.Empty;

    [ObservableProperty]
    private string _kindLabel = string.Empty;

    [ObservableProperty]
    private string _targetLabel = string.Empty;

    /// <param name="existing">The definition to edit, or null to add a new one.</param>
    /// <param name="isDuplicate">Tells whether another definition already has the candidate's name and target.</param>
    public CustomFieldEditorViewModel(ILocalizer localizer, CustomFieldDefinition? existing, Func<CustomFieldDefinition, bool> isDuplicate)
        : base(localizer)
    {
        _localizer = localizer;
        _isDuplicate = isDuplicate;
        IsNew = existing is null;

        Kinds = EnumText.Options<CustomFieldKind>(localizer);
        Targets = EnumText.Options<CustomFieldTarget>(localizer);
        SelectedKind = Kinds.First(k => k.Value.Equals(existing?.Kind ?? CustomFieldKind.Text));
        SelectedTarget = Targets.First(t => t.Value.Equals(existing?.Target ?? CustomFieldTarget.Bill));
        Name = existing?.Name ?? string.Empty;
        RefreshTexts();
    }

    public bool IsNew { get; }

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

    public int SelectedTargetIndex
    {
        get => SelectedTarget is null ? -1 : Targets.ToList().IndexOf(SelectedTarget);
        set
        {
            if (value >= 0 && value < Targets.Count)
            {
                SelectedTarget = Targets[value];
            }
        }
    }

    public string NameError => Errors.TryGetValue(nameof(CustomFieldDefinition.Name), out var message) ? message : string.Empty;

    /// <summary>The validated definition (name, kind, target only); meaningful only after <see cref="EditorDialogViewModel.Saved"/>.</summary>
    public CustomFieldDefinition? Result { get; private set; }

    protected override bool TryApply()
    {
        var candidate = new CustomFieldDefinition
        {
            Name = Name.Trim(),
            Kind = SelectedKind?.As<CustomFieldKind>() ?? CustomFieldKind.Text,
            Target = SelectedTarget?.As<CustomFieldTarget>() ?? CustomFieldTarget.Bill,
        };

        var errors = CustomFieldValidator.Validate(candidate).Errors.ToList();
        if (errors.Count == 0 && _isDuplicate(candidate))
        {
            errors.Add(new ValidationError(ErrorCodes.CustomFieldNameDuplicate, nameof(CustomFieldDefinition.Name)));
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
        var target = SelectedTarget?.Value;
        Kinds = EnumText.Options<CustomFieldKind>(_localizer);
        Targets = EnumText.Options<CustomFieldTarget>(_localizer);
        if (kind is not null)
        {
            SelectedKind = Kinds.First(k => k.Value.Equals(kind));
        }

        if (target is not null)
        {
            SelectedTarget = Targets.First(t => t.Value.Equals(target));
        }

        Title = _localizer[IsNew ? "CustomFieldNew" : "CustomFieldEdit"];
        NameLabel = _localizer["CustomFieldName"];
        KindLabel = _localizer["CustomFieldKindLabel"];
        TargetLabel = _localizer["CustomFieldTargetLabel"];
    }
}
