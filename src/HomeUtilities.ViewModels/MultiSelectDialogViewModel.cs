using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>One check box of a <see cref="MultiSelectDialogViewModel"/>.</summary>
public sealed partial class MultiSelectItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Creates the item.</summary>
    /// <param name="id">What the item stands for (a property or category id).</param>
    /// <param name="text">The display name.</param>
    /// <param name="color">Colour dot as <c>#RRGGBB</c>; empty = no dot.</param>
    /// <param name="isSelected">The initial state.</param>
    public MultiSelectItemViewModel(Guid id, string text, string color, bool isSelected)
    {
        Id = id;
        Text = text;
        Color = color;
        _isSelected = isSelected;
    }

    /// <summary>What the item stands for.</summary>
    public Guid Id { get; }

    /// <summary>The display name.</summary>
    public string Text { get; }

    /// <summary>Colour dot as <c>#RRGGBB</c>; empty = no dot.</summary>
    public string Color { get; }

    /// <summary>True if the item has a colour dot.</summary>
    public bool HasColor => Color.Length > 0;
}

/// <summary>
/// A dialog with a check box per item, All / None at the top and OK / Cancel. At least one item must stay selected: OK is
/// disabled otherwise. Reusable for any "pick several" filter (reports: properties, categories).
/// </summary>
public sealed partial class MultiSelectDialogViewModel : EditorDialogViewModel
{
    /// <summary>Creates the dialog; <paramref name="selected"/> are the ids checked at the start.</summary>
    public MultiSelectDialogViewModel(ILocalizer localizer, string title, IEnumerable<MultiSelectItemViewModel> items, IReadOnlyCollection<Guid> selected)
        : base(localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(selected);

        Title = title;
        Items = [.. items];
        foreach (var item in Items)
        {
            item.IsSelected = selected.Contains(item.Id);
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MultiSelectItemViewModel.IsSelected))
                {
                    OnSelectionChanged();
                }
            };
        }

        AllText = localizer["ReportSelectAll"];
        NoneText = localizer["ReportSelectNone"];
        OkText = localizer["Dialog_Ok"];
    }

    /// <summary>The check boxes.</summary>
    public IReadOnlyList<MultiSelectItemViewModel> Items { get; }

    /// <summary>Text of the "All" button.</summary>
    public string AllText { get; }

    /// <summary>Text of the "None" button.</summary>
    public string NoneText { get; }

    /// <summary>Text of the confirm button.</summary>
    public string OkText { get; }

    /// <summary>How many items are checked.</summary>
    public int SelectedCount => Items.Count(i => i.IsSelected);

    /// <summary>True if at least one item is checked (OK is enabled).</summary>
    public bool HasSelection => SelectedCount > 0;

    /// <summary>The ids of the checked items; meaningful after the dialog was confirmed.</summary>
    public HashSet<Guid> SelectedIds => [.. Items.Where(i => i.IsSelected).Select(i => i.Id)];

    protected override bool TryApply() => HasSelection;

    [RelayCommand]
    private void SelectAll() => SetAll(true);

    [RelayCommand]
    private void SelectNone() => SetAll(false);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Confirm()
    {
        if (HasSelection)
        {
            CloseSaved();
        }
    }

    private void SetAll(bool value)
    {
        foreach (var item in Items)
        {
            item.IsSelected = value;
        }
    }

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        ConfirmCommand.NotifyCanExecuteChanged();
    }
}
