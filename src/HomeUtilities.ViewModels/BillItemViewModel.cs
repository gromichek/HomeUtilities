using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Billing;

namespace HomeUtilities.ViewModels;

/// <summary>One row of the bills list.</summary>
public sealed partial class BillItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public BillItemViewModel(
        Bill bill,
        string title,
        string supplierName,
        string color,
        BillState state,
        string currencySymbol,
        ILocalizer localizer,
        bool isHighlighted,
        Action<Guid> select,
        Action<Guid> markPaid)
    {
        Id = bill.Id;
        ContractId = bill.ContractId;
        PeriodText = BillText.Period(bill.Period);
        Title = title;
        SupplierName = supplierName;
        Color = color;
        TotalText = BillText.Money(bill.Gross, currencySymbol);
        Badge = BillBadge.KindOf(state);
        BadgeText = BillBadge.TextOf(localizer, state, currencySymbol);
        IsHighlighted = isHighlighted;
        CanMarkPaid = state.Open != 0 && Math.Sign(state.Open) == Math.Sign(bill.Gross);
        MarkPaidText = localizer["BillMarkPaid"];

        var comparison = BillComparison.Compare(bill, bill.CalculatedSnapshot, BillComparison.MeteredQuantity(bill.CalculatedSnapshot));
        if (comparison.Difference is { } difference)
        {
            DifferenceText = BillText.Difference(difference, comparison.DifferencePercent, currencySymbol);
            var tone = BillComparison.ToneOf(comparison.DifferencePercent);
            DifferenceHigher = tone == DifferenceTone.Higher;
            DifferenceLower = tone == DifferenceTone.Lower;
        }

        SelectCommand = new RelayCommand(() => select(Id));
        MarkPaidCommand = new RelayCommand(() => markPaid(Id));
    }

    public Guid Id { get; }

    public Guid? ContractId { get; }

    /// <summary>"10.2026" — the month the bill belongs to.</summary>
    public string PeriodText { get; }

    /// <summary>The contract's title, or the title of a one-off expense.</summary>
    public string Title { get; }

    public string SupplierName { get; }

    public bool HasSupplier => SupplierName.Length > 0;

    /// <summary>Colour of the category as <c>#RRGGBB</c>.</summary>
    public string Color { get; }

    /// <summary>"46,02 €".</summary>
    public string TotalText { get; }

    public BillBadgeKind Badge { get; }

    public string BadgeText { get; }

    public bool IsBadgePaid => Badge is BillBadgeKind.Paid or BillBadgeKind.Prepaid;

    public bool IsBadgePartial => Badge == BillBadgeKind.PartiallyPaid;

    public bool IsBadgeOverdue => Badge == BillBadgeKind.Overdue;

    /// <summary>"+2,15 € (+3,4 %)", empty when the bill has no calculation to compare with.</summary>
    public string DifferenceText { get; } = string.Empty;

    public bool HasDifference => DifferenceText.Length > 0;

    /// <summary>More than 5 % above the calculation (red).</summary>
    public bool DifferenceHigher { get; }

    /// <summary>More than 5 % below the calculation (green).</summary>
    public bool DifferenceLower { get; }

    /// <summary>The contract the screen was opened for ("All bills" of a contract).</summary>
    public bool IsHighlighted { get; }

    public bool CanMarkPaid { get; }

    public string MarkPaidText { get; }

    public IRelayCommand SelectCommand { get; }

    public IRelayCommand MarkPaidCommand { get; }
}
