using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Tariffs;

/// <summary>A VAT percentage that applies from a given date onwards.</summary>
public class VatRateVersion : SyncEntity, IVersioned
{
    /// <summary>Owning VAT rate.</summary>
    public Guid VatRateId { get; set; }

    /// <summary>First date on which <see cref="Percent"/> applies.</summary>
    public DateOnly ValidFrom { get; set; }

    /// <summary>VAT in percent, 0–100 (e.g. 21).</summary>
    public decimal Percent { get; set; }
}
