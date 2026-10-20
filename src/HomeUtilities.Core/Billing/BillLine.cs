using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Billing;

/// <summary>An optional detail line of a bill.</summary>
public class BillLine : SyncEntity
{
    /// <summary>The bill the line belongs to.</summary>
    public Guid BillId { get; set; }

    /// <summary>What the line is for.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Billed quantity.</summary>
    public decimal? Quantity { get; set; }

    /// <summary>Unit of the quantity, e.g. "m3".</summary>
    public string? Unit { get; set; }

    /// <summary>Price per unit.</summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>Amount excluding VAT.</summary>
    public decimal? Net { get; set; }

    /// <summary>VAT rate in percent.</summary>
    public decimal? VatPercent { get; set; }

    /// <summary>VAT amount.</summary>
    public decimal? Vat { get; set; }

    /// <summary>Total including VAT.</summary>
    public decimal Gross { get; set; }

    /// <summary>Category of the line if it differs from the bill's.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Values of the line-level custom fields.</summary>
    public List<CustomFieldValue> CustomFields { get; set; } = [];

    /// <summary>Free-text comment.</summary>
    public string? Note { get; set; }

    /// <summary>Display order.</summary>
    public int SortOrder { get; set; }
}
