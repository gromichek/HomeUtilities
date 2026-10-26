using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>One installment in the "Tax by year" card: number and due date, amount and the payment badge; tap to open the bill.</summary>
public sealed class TaxInstallmentRowViewModel
{
    public TaxInstallmentRowViewModel(Guid billId, string titleText, string amountText, BillBadgeKind badge, string badgeText, Action open)
    {
        BillId = billId;
        TitleText = titleText;
        AmountText = amountText;
        Badge = badge;
        BadgeText = badgeText;
        OpenCommand = new RelayCommand(open);
    }

    public Guid BillId { get; }

    /// <summary>"1/4 · due 31.03.2026".</summary>
    public string TitleText { get; }

    public string AmountText { get; }

    public BillBadgeKind Badge { get; }

    public string BadgeText { get; }

    public bool IsBadgePaid => Badge is BillBadgeKind.Paid or BillBadgeKind.Prepaid;

    public bool IsBadgePartial => Badge == BillBadgeKind.PartiallyPaid;

    public bool IsBadgeOverdue => Badge == BillBadgeKind.Overdue;

    public IRelayCommand OpenCommand { get; }
}

/// <summary>One tax year in the "Tax by year" card: total, installments, paid and remaining.</summary>
public sealed class TaxYearViewModel
{
    public TaxYearViewModel(
        int year, string headerText, string summaryText, IReadOnlyList<TaxInstallmentRowViewModel> installments, bool canPayYear,
        string payYearText, Action payYear)
    {
        Year = year;
        HeaderText = headerText;
        SummaryText = summaryText;
        Installments = installments;
        CanPayYear = canPayYear;
        PayYearText = payYearText;
        PayYearCommand = new RelayCommand(payYear);
    }

    public int Year { get; }

    /// <summary>"2026 · 96,40 €".</summary>
    public string HeaderText { get; }

    /// <summary>"Paid 72,30 € · remaining 24,10 €".</summary>
    public string SummaryText { get; }

    public IReadOnlyList<TaxInstallmentRowViewModel> Installments { get; }

    /// <summary>"Pay whole year" is offered while something is open.</summary>
    public bool CanPayYear { get; }

    public string PayYearText { get; }

    public IRelayCommand PayYearCommand { get; }
}
