using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Billing;

/// <summary>How a recurring or repeated payment looks and when it comes (DATA_MODEL §8).</summary>
public class BillTemplate : SyncEntity
{
    /// <summary>Name shown in lists and pickers, e.g. "Internet + TV".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Property of the bills.</summary>
    public Guid PropertyId { get; set; }

    /// <summary>Category of the bills.</summary>
    public Guid CategoryId { get; set; }

    /// <summary>Contract of the bills, optional.</summary>
    public Guid? ContractId { get; set; }

    /// <summary>Supplier of the bills, optional.</summary>
    public Guid? SupplierId { get; set; }

    /// <summary>Default bill title.</summary>
    public string? Title { get; set; }

    /// <summary>Default amount excluding VAT.</summary>
    public decimal? Net { get; set; }

    /// <summary>Default VAT rate in percent.</summary>
    public decimal? VatPercent { get; set; }

    /// <summary>Default total; with none of the three amounts set the bill is created with 0 for the user to fill in.</summary>
    public decimal? Gross { get; set; }

    /// <summary>Default detail lines.</summary>
    public List<BillTemplateLine> Lines { get; set; } = [];

    /// <summary>Custom fields the bills carry; their values start empty.</summary>
    public List<CustomFieldDefinition> CustomFields { get; set; } = [];

    /// <summary>When bills are created.</summary>
    public Schedule Schedule { get; set; } = new();

    /// <summary>In arrears (bill for a past month) or in advance (prepaid).</summary>
    public BillingTiming Timing { get; set; } = BillingTiming.InArrears;

    /// <summary>The due date is the issue date plus this many days.</summary>
    public int? DueDayOffset { get; set; }

    /// <summary>Remind this many days before the due date (used by the reminders of T-028).</summary>
    public int? RemindDaysBefore { get; set; }

    /// <summary>Inactive templates create no drafts and are not offered in "From template".</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The last period a draft was created for; keeps the generation from repeating itself.</summary>
    public YearMonth? LastGeneratedPeriod { get; set; }

    /// <summary>Free-text comment.</summary>
    public string? Notes { get; set; }
}
