using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>One row of the contracts card in the property editor.</summary>
public sealed class ContractRowViewModel
{
    public ContractRowViewModel(
        Contract contract, string supplierName, string color, DateOnly today, ILocalizer localizer, Action<Guid> onSelect)
    {
        Id = contract.Id;
        Title = contract.Title;
        SupplierName = supplierName;
        ServiceText = EnumText.Get(localizer, contract.Service);
        SubtitleText = supplierName.Length == 0 ? ServiceText : $"{supplierName} · {ServiceText}";
        Color = color;
        IsArchived = contract.IsArchived;
        ArchivedText = localizer["ContractArchivedBadge"];
        IsEnded = contract.ValidTo is { } end && end < today;
        EndedText = contract.ValidTo is { } validTo ? localizer.Get("ContractEnded", validTo) : string.Empty;
        SelectCommand = new RelayCommand(() => onSelect(Id));
    }

    public Guid Id { get; }

    public string Title { get; }

    public string SupplierName { get; }

    public string ServiceText { get; }

    /// <summary>Supplier and service kind on one line, e.g. "Water utility · Cold water".</summary>
    public string SubtitleText { get; }

    /// <summary>Colour of the contract's category as <c>#RRGGBB</c>, for the colour dot.</summary>
    public string Color { get; }

    public bool IsArchived { get; }

    public string ArchivedText { get; }

    public bool IsEnded { get; }

    public string EndedText { get; }

    public IRelayCommand SelectCommand { get; }
}
