using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>One row of the property list.</summary>
public sealed partial class PropertyItemViewModel : ObservableObject, ISelectableListItem
{
    [ObservableProperty]
    private bool _isSelected;

    public PropertyItemViewModel(Property property, DateOnly today, ILocalizer localizer, Action<PropertyItemViewModel> onSelect)
    {
        Id = property.Id;
        Name = property.Name;
        KindText = EnumText.Get(localizer, property.Kind);
        Address = property.Address ?? string.Empty;
        IsArchived = property.IsArchived;
        ArchivedText = localizer["PropertyArchivedBadge"];

        var residents = property.GetParameter(PropertyParameterKind.DeclaredResidents, today);
        var area = property.GetParameter(PropertyParameterKind.TotalArea, today);
        var parts = new List<string>();
        if (residents is { } r)
        {
            parts.Add(localizer.Get("PropertySummaryResidents", DecimalInput.Format(r, CultureInfo.CurrentCulture, 0)));
        }

        if (area is { } a)
        {
            parts.Add(localizer.Get("PropertySummaryArea", DecimalInput.Format(a, CultureInfo.CurrentCulture, 2)));
        }

        SummaryText = string.Join(" · ", parts);
        SelectCommand = new RelayCommand(() => onSelect(this));
    }

    public Guid Id { get; }

    public string Name { get; }

    public string KindText { get; }

    public string Address { get; }

    public bool IsArchived { get; }

    public string ArchivedText { get; }

    /// <summary>Current residents and area as of today, e.g. "Residents: 2 · Area: 52.3 m²".</summary>
    public string SummaryText { get; }

    public IRelayCommand SelectCommand { get; }
}
