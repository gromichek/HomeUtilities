using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>Dialog that adds a Day / Night / Peak / Export register to a meter.</summary>
public sealed partial class RegisterEditorViewModel : EditorDialogViewModel
{
    private readonly ILocalizer _localizer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedKindIndex))]
    private IReadOnlyList<EnumOption> _kinds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedKindIndex), nameof(ExportHint), nameof(IsExport))]
    private EnumOption? _selectedKind;

    [ObservableProperty]
    private string _kindLabel = string.Empty;

    /// <param name="localizer">Localizer.</param>
    /// <param name="usedKinds">The kinds the meter already has; they are not offered again.</param>
    public RegisterEditorViewModel(ILocalizer localizer, IReadOnlySet<RegisterKind> usedKinds)
        : base(localizer)
    {
        _localizer = localizer;
        _kinds =
        [
            .. EnumText.Options<RegisterKind>(localizer)
                .Where(o => o.As<RegisterKind>() != RegisterKind.Total && !usedKinds.Contains(o.As<RegisterKind>())),
        ];
        _selectedKind = _kinds.FirstOrDefault();
        RefreshTexts();
    }

    /// <summary>The register added by the dialog; meaningful only after <see cref="EditorDialogViewModel.Saved"/>.</summary>
    public (RegisterKind Kind, EnergyDirection Direction)? Result { get; private set; }

    public bool IsExport => SelectedKind?.Value is RegisterKind.Export;

    /// <summary>Explains that an export register always counts energy fed into the grid.</summary>
    public string ExportHint => IsExport ? _localizer["RegisterExportHint"] : string.Empty;

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

    protected override bool TryApply()
    {
        if (SelectedKind is not { } option)
        {
            return false;
        }

        var kind = option.As<RegisterKind>();
        Result = (kind, kind == RegisterKind.Export ? EnergyDirection.Export : EnergyDirection.Import);
        return true;
    }

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        var kind = SelectedKind?.Value;
        Kinds =
        [
            .. EnumText.Options<RegisterKind>(_localizer)
                .Where(o => o.As<RegisterKind>() != RegisterKind.Total && Kinds.Any(k => k.Value.Equals(o.Value))),
        ];
        SelectedKind = kind is null ? Kinds.FirstOrDefault() : Kinds.FirstOrDefault(k => k.Value.Equals(kind));
        Title = _localizer["RegisterNew"];
        KindLabel = _localizer["RegisterKindLabel"];
        OnPropertyChanged(nameof(ExportHint));
    }
}
