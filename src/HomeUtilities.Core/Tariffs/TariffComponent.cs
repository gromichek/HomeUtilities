using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tariffs;

/// <summary>One priced line of a contract, e.g. "Water supply" or "Distribution".</summary>
public class TariffComponent : SyncEntity
{
    /// <summary>Owning contract.</summary>
    public Guid ContractId { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Service this line counts toward in reports.</summary>
    public ServiceKind ChargeKind { get; set; }

    /// <summary>What the quantity is based on.</summary>
    public ChargeBasis Basis { get; set; }

    /// <summary>Registers that are counted; empty means all import registers. Only for PerUnit and NetExport.</summary>
    public List<RegisterKind> RegisterKinds { get; set; } = [];

    /// <summary>Meters that are counted; empty means all contract meters. Only for PerUnit and NetExport.</summary>
    public List<Guid> MeterIds { get; set; } = [];

    /// <summary>Code of the VAT catalog entry; may be empty when <see cref="VatPercentOverride"/> is set.</summary>
    public string VatCode { get; set; } = string.Empty;

    /// <summary>Explicit VAT percent that wins over the catalog.</summary>
    public decimal? VatPercentOverride { get; set; }

    /// <summary>Prices over time.</summary>
    public List<TariffRate> Rates { get; set; } = [];

    /// <summary>Position among the components of the contract.</summary>
    public int SortOrder { get; set; }

    /// <summary>The rate valid on <paramref name="date"/>, or null if none applies yet.</summary>
    public TariffRate? RateAt(DateOnly date) => Versioned.At(Rates, date);
}
