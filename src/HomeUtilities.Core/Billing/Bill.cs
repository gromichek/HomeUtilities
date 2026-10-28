using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Billing;

/// <summary>A supplier invoice or any other expense.</summary>
public class Bill : SyncEntity
{
    /// <summary>Property the bill is for.</summary>
    public Guid PropertyId { get; set; }

    /// <summary>Category the bill is reported under.</summary>
    public Guid CategoryId { get; set; }

    /// <summary>Contract of the bill; null for a one-off expense.</summary>
    public Guid? ContractId { get; set; }

    /// <summary>Supplier, optional for one-off expenses.</summary>
    public Guid? SupplierId { get; set; }

    /// <summary>The month the bill belongs to: the month of service or readings, not of issue.</summary>
    public YearMonth Period { get; set; }

    /// <summary>First day of the service period of a non-monthly bill.</summary>
    public DateOnly? ServiceFrom { get; set; }

    /// <summary>Last day of the service period of a non-monthly bill.</summary>
    public DateOnly? ServiceTo { get; set; }

    /// <summary>What kind of bill this is.</summary>
    public BillKind Kind { get; set; }

    /// <summary>Free description, required for one-off expenses.</summary>
    public string? Title { get; set; }

    /// <summary>Invoice number.</summary>
    public string? Number { get; set; }

    /// <summary>Date the bill was issued.</summary>
    public DateOnly? IssuedOn { get; set; }

    /// <summary>Date the bill is due.</summary>
    public DateOnly? DueOn { get; set; }

    /// <summary>Amount excluding VAT.</summary>
    public decimal? Net { get; set; }

    /// <summary>VAT rate in percent.</summary>
    public decimal? VatPercent { get; set; }

    /// <summary>VAT amount.</summary>
    public decimal? Vat { get; set; }

    /// <summary>Total including VAT; always set.</summary>
    public decimal Gross { get; set; }

    /// <summary>Currency of all amounts.</summary>
    public Currency Currency { get; set; } = Currency.EUR;

    /// <summary>Optional detail lines.</summary>
    public List<BillLine> Lines { get; set; } = [];

    /// <summary>Values of the bill-level custom fields.</summary>
    public List<CustomFieldValue> CustomFields { get; set; } = [];

    /// <summary>Quantity the supplier billed, to compare with the metered one (water losses).</summary>
    public decimal? ConsumptionOnBill { get; set; }

    /// <summary>Quantities typed from the bill for the QuantityFromBill components of the contract.</summary>
    public List<BillQuantity> BillQuantities { get; set; } = [];

    /// <summary>The app's own calculation at the time the bill was entered.</summary>
    public CalculatedCharge? CalculatedSnapshot { get; set; }

    /// <summary>The bill template the bill was created from.</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>
    /// True while the bill was created from a template and the owner has not confirmed it yet. Drafts do not count in
    /// statuses, balances, payments, reports or the Overview money.
    /// </summary>
    public bool IsDraft { get; set; }

    /// <summary>Attached documents (PDF, photo).</summary>
    public List<Guid> AttachmentIds { get; set; } = [];

    /// <summary>Free-text comment.</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Creates a bill for a contract: copies property, category, supplier and currency; the kind follows the
    /// billing mode (Equalized, TaxInstallment for Assessed, otherwise Regular).
    /// </summary>
    public static Bill CreateFor(Contract contract, YearMonth period, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(clock);

        var bill = new Bill
        {
            PropertyId = contract.PropertyId,
            CategoryId = contract.CategoryId,
            ContractId = contract.Id,
            SupplierId = contract.SupplierId,
            Period = period,
            Currency = contract.Currency,
            Kind = contract.BillingMode switch
            {
                BillingMode.Equalized => BillKind.Equalized,
                BillingMode.Assessed => BillKind.TaxInstallment,
                _ => BillKind.Regular,
            },
        };
        bill.Touch(clock);
        return bill;
    }

    /// <summary>Creates a one-off expense without a contract.</summary>
    public static Bill CreateOneOff(Guid propertyId, Guid categoryId, string title, YearMonth period, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var bill = new Bill
        {
            PropertyId = propertyId,
            CategoryId = categoryId,
            Title = title,
            Period = period,
            Kind = BillKind.OneOff,
        };
        bill.Touch(clock);
        return bill;
    }

    /// <summary>The period a new bill defaults to: previous month in arrears, next month in advance.</summary>
    public static YearMonth DefaultPeriod(Contract contract, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(contract);

        return YearMonth.From(today).AddMonths(contract.BillingTiming == BillingTiming.InAdvance ? 1 : -1);
    }
}
