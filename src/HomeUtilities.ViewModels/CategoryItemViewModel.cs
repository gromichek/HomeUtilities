using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>One row of the category list.</summary>
public sealed partial class CategoryItemViewModel : ObservableObject, ISelectableListItem
{
    [ObservableProperty]
    private bool _isSelected;

    public CategoryItemViewModel(Category category, ILocalizer localizer, Action<Guid> onSelect)
    {
        Id = category.Id;
        DisplayName = CategoryNames.DisplayName(category, localizer);
        Color = category.Color;
        IsBuiltIn = category.IsBuiltIn;
        BuiltInText = localizer["CategoryBuiltInBadge"];
        IsArchived = category.IsArchived;
        ArchivedText = localizer["CategoryArchivedBadge"];
        SelectCommand = new RelayCommand(() => onSelect(Id));
    }

    public Guid Id { get; }

    public string DisplayName { get; }

    /// <summary>Colour as <c>#RRGGBB</c> for the colour dot.</summary>
    public string Color { get; }

    public bool IsBuiltIn { get; }

    public string BuiltInText { get; }

    public bool IsArchived { get; }

    public string ArchivedText { get; }

    public IRelayCommand SelectCommand { get; }
}
