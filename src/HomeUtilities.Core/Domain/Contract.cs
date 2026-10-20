using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Payments;

namespace HomeUtilities.Core.Domain;

/// <summary>A customer account with a supplier for one property.</summary>
public class Contract : SyncEntity
{
    /// <summary>Property the contract belongs to.</summary>
    public Guid PropertyId { get; set; }

    /// <summary>Supplier of the contract.</summary>
    public Guid SupplierId { get; set; }

    /// <summary>Main service of the contract.</summary>
    public ServiceKind Service { get; set; }

    /// <summary>Display title, e.g. "Water".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Category the contract's charges are reported under.</summary>
    public Guid CategoryId { get; set; }

    /// <summary>Customer (account) number.</summary>
    public string? CustomerNumber { get; set; }

    /// <summary>Contract number.</summary>
    public string? ContractNumber { get; set; }

    /// <summary>Optional reminder of the portal user name. Passwords are never stored.</summary>
    public string? PortalLogin { get; set; }

    /// <summary>First day the contract applies.</summary>
    public DateOnly ValidFrom { get; set; }

    /// <summary>Last day the contract applies; null if open-ended.</summary>
    public DateOnly? ValidTo { get; set; }

    /// <summary>Currency of the amounts.</summary>
    public Currency Currency { get; set; } = Currency.EUR;

    /// <summary>How the contract is billed.</summary>
    public BillingMode BillingMode { get; set; }

    /// <summary>First day of the month of the reading submission window.</summary>
    public int? ReadingDayFrom { get; set; }

    /// <summary>Last day of the reading submission window; may be smaller than the first day (wraps the month end).</summary>
    public int? ReadingDayTo { get; set; }

    /// <summary>Usual day of the month the bill arrives.</summary>
    public int? BillDay { get; set; }

    /// <summary>Usual due day of the month.</summary>
    public int? PaymentDueDay { get; set; }

    /// <summary>Whether bills are issued after the service month or paid in advance.</summary>
    public BillingTiming BillingTiming { get; set; } = BillingTiming.InArrears;

    /// <summary>Custom fields that reappear on every bill of the contract.</summary>
    public List<CustomFieldDefinition> CustomFields { get; set; } = [];

    /// <summary>Contract-specific links; they override supplier links per kind.</summary>
    public List<Link> Links { get; set; } = [];

    /// <summary>Contract-level payment details; each field overrides the supplier's.</summary>
    public PaymentDetails? PaymentDetails { get; set; }

    /// <summary>Free-form notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Archived contracts are hidden by default.</summary>
    public bool IsArchived { get; set; }

    /// <summary>True if the contract is not deleted and applies on the given date (both bounds inclusive).</summary>
    public bool IsActiveOn(DateOnly date) =>
        !IsDeleted && ValidFrom <= date && (ValidTo is null || date <= ValidTo);

    /// <summary>
    /// Contract links first, then supplier links whose kind is not present among the contract links.
    /// <see cref="LinkKind.Other"/> links are never deduplicated. Deleted links are excluded;
    /// each group is ordered by <see cref="Link.SortOrder"/>.
    /// </summary>
    public IReadOnlyList<Link> GetEffectiveLinks(Supplier supplier)
    {
        ArgumentNullException.ThrowIfNull(supplier);

        var own = Links.Where(l => !l.IsDeleted).OrderBy(l => l.SortOrder).ToList();
        var ownKinds = own.Select(l => l.Kind).ToHashSet();
        var inherited = supplier.Links
            .Where(l => !l.IsDeleted && (l.Kind == LinkKind.Other || !ownKinds.Contains(l.Kind)))
            .OrderBy(l => l.SortOrder);

        return [.. own, .. inherited];
    }

    /// <summary>
    /// Dates of the reading window for the given period, or null if the days are not set.
    /// Days beyond the month length are clamped; a window with From &gt; To ends in the next month.
    /// </summary>
    public (DateOnly From, DateOnly To)? GetReadingWindow(YearMonth period)
    {
        if (ReadingDayFrom is not { } from || ReadingDayTo is not { } to)
        {
            return null;
        }

        return ReadingWindow.For(period, from, to);
    }
}
