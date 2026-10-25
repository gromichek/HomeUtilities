using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>One bill in the "Bills" card of the contract editor: month, total and status; tap to open the bill.</summary>
public sealed class ContractBillRowViewModel
{
    public ContractBillRowViewModel(Guid id, string periodText, string totalText, BillBadgeKind badge, string badgeText, Action open)
    {
        Id = id;
        PeriodText = periodText;
        TotalText = totalText;
        Badge = badge;
        BadgeText = badgeText;
        OpenCommand = new RelayCommand(open);
    }

    public Guid Id { get; }

    /// <summary>"10.2026".</summary>
    public string PeriodText { get; }

    /// <summary>"46,02 €".</summary>
    public string TotalText { get; }

    public BillBadgeKind Badge { get; }

    public string BadgeText { get; }

    public bool IsBadgePaid => Badge is BillBadgeKind.Paid or BillBadgeKind.Prepaid;

    public bool IsBadgePartial => Badge == BillBadgeKind.PartiallyPaid;

    public bool IsBadgeOverdue => Badge == BillBadgeKind.Overdue;

    public IRelayCommand OpenCommand { get; }
}
