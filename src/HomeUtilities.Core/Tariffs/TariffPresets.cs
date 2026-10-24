using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tariffs;

/// <summary>The quick-start sets of tariff components offered on an empty tariff card.</summary>
public enum TariffPreset
{
    /// <summary>Water supply and sewerage, both per cubic metre.</summary>
    WaterSewerage = 0,

    /// <summary>Water heating (hot meters) and heating from the bill.</summary>
    WaterHeatingAndHeating = 1,

    /// <summary>Energy and distribution, both per kWh.</summary>
    Electricity = 2,

    /// <summary>A fee per square metre, price including VAT.</summary>
    AreaFee = 3,

    /// <summary>A fee per declared resident.</summary>
    PerResident = 4,

    /// <summary>A fixed monthly amount, price including VAT.</summary>
    FixedMonthly = 5,
}

/// <summary>
/// Creates the components of a <see cref="TariffPreset"/>. They carry no prices: the user enters those. The presets are code,
/// not data.
/// </summary>
public static class TariffPresets
{
    /// <summary>Name keys passed to the <c>nameOf</c> callback of <see cref="Create"/>.</summary>
    public static class NameKeys
    {
        public const string WaterSupply = "WaterSupply";
        public const string Sewerage = "Sewerage";
        public const string WaterHeating = "WaterHeating";
        public const string Heating = "Heating";
        public const string Energy = "Energy";
        public const string Distribution = "Distribution";
        public const string AreaFee = "AreaFee";
        public const string PerResident = "PerResident";
        public const string FixedMonthly = "FixedMonthly";
    }

    /// <summary>Code of the standard VAT entry of the catalog.</summary>
    public const string StandardVatCode = "LV-STANDARD";

    /// <summary>Code of the reduced VAT entry of the catalog.</summary>
    public const string ReducedVatCode = "LV-REDUCED";

    /// <summary>Creates new components, ordered, with sort orders starting at <paramref name="firstSortOrder"/>.</summary>
    /// <param name="preset">The preset.</param>
    /// <param name="contractId">Contract the components belong to.</param>
    /// <param name="contractService">Service of the contract; the charge kind of the generic presets.</param>
    /// <param name="nameOf">Maps a key of <see cref="NameKeys"/> to the localized component name.</param>
    /// <param name="hotMeterIds">Hot-water meters of the contract; the water heating component counts only these (empty = all meters).</param>
    /// <param name="firstSortOrder">Sort order of the first component.</param>
    public static IReadOnlyList<TariffComponent> Create(
        TariffPreset preset,
        Guid contractId,
        ServiceKind contractService,
        Func<string, string> nameOf,
        IReadOnlyCollection<Guid> hotMeterIds,
        int firstSortOrder = 0)
    {
        ArgumentNullException.ThrowIfNull(nameOf);
        ArgumentNullException.ThrowIfNull(hotMeterIds);

        TariffComponent Component(string key, ServiceKind kind, ChargeBasis basis, string vat = StandardVatCode) =>
            new() { ContractId = contractId, Name = nameOf(key), ChargeKind = kind, Basis = basis, VatCode = vat };

        List<TariffComponent> components = preset switch
        {
            TariffPreset.WaterSewerage =>
            [
                Component(NameKeys.WaterSupply, ServiceKind.ColdWater, ChargeBasis.PerUnit),
                Component(NameKeys.Sewerage, ServiceKind.Sewerage, ChargeBasis.PerUnit),
            ],
            TariffPreset.WaterHeatingAndHeating =>
            [
                Component(NameKeys.WaterHeating, ServiceKind.WaterHeating, ChargeBasis.PerUnit, ReducedVatCode),
                Component(NameKeys.Heating, ServiceKind.Heating, ChargeBasis.FromBill, ReducedVatCode),
            ],
            TariffPreset.Electricity =>
            [
                Component(NameKeys.Energy, ServiceKind.Electricity, ChargeBasis.PerUnit),
                Component(NameKeys.Distribution, ServiceKind.Electricity, ChargeBasis.PerUnit),
            ],
            TariffPreset.AreaFee => [Component(NameKeys.AreaFee, contractService, ChargeBasis.PerArea)],
            TariffPreset.PerResident => [Component(NameKeys.PerResident, contractService, ChargeBasis.PerResident)],
            TariffPreset.FixedMonthly => [Component(NameKeys.FixedMonthly, contractService, ChargeBasis.FixedMonthly)],
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown tariff preset."),
        };

        if (preset == TariffPreset.WaterHeatingAndHeating)
        {
            components[0].MeterIds = [.. hotMeterIds];
        }

        for (var i = 0; i < components.Count; i++)
        {
            components[i].SortOrder = firstSortOrder + i;
        }

        return components;
    }
}
