using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Tariffs;

/// <summary>An entry of the VAT catalog: a code with a time-versioned percentage.</summary>
public class VatRate : SyncEntity
{
    /// <summary>Unique upper-case code, e.g. <c>LV-STANDARD</c>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Percentages over time.</summary>
    public List<VatRateVersion> Versions { get; set; } = [];

    /// <summary>VAT percent valid on <paramref name="date"/>, or null if there is no version yet.</summary>
    public decimal? PercentAt(DateOnly date) => Versioned.At(Versions, date)?.Percent;
}
