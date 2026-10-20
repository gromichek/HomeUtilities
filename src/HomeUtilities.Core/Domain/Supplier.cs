using HomeUtilities.Core.Common;
using HomeUtilities.Core.Payments;

namespace HomeUtilities.Core.Domain;

/// <summary>A utility or service provider.</summary>
public class Supplier : SyncEntity
{
    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Company registration number.</summary>
    public string? RegistrationNumber { get; set; }

    /// <summary>Phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>E-mail address.</summary>
    public string? Email { get; set; }

    /// <summary>Free-form notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Default bank details for payments to this supplier.</summary>
    public PaymentDetails? PaymentDetails { get; set; }

    /// <summary>Web links of the supplier.</summary>
    public List<Link> Links { get; set; } = [];
}
