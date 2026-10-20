using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels;

/// <summary>One row of the supplier list.</summary>
public sealed partial class SupplierItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public SupplierItemViewModel(Supplier supplier, ILocalizer localizer, Action<Guid> onSelect)
    {
        Id = supplier.Id;
        Name = supplier.Name;
        var linkCount = supplier.Links.Count(l => !l.IsDeleted);
        LinksText = linkCount == 0 ? string.Empty : localizer.Get("SupplierLinksCount", linkCount);
        HasPaymentDetails = supplier.PaymentDetails is not null;
        PaymentDetailsText = localizer["SupplierHasPaymentDetails"];
        SelectCommand = new RelayCommand(() => onSelect(Id));
    }

    public Guid Id { get; }

    public string Name { get; }

    /// <summary>"Links: 2", empty when the supplier has none.</summary>
    public string LinksText { get; }

    public bool HasLinks => LinksText.Length > 0;

    public bool HasPaymentDetails { get; }

    public string PaymentDetailsText { get; }

    public IRelayCommand SelectCommand { get; }
}
