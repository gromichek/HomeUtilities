using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.Core.Reports;

/// <summary>
/// Everything the reports need, loaded once (rows that are not deleted). The calculated charges are produced on demand from
/// the loaded tariffs and readings and remembered, so a report calculates each contract and month at most once.
/// Not thread-safe: use one instance per report run.
/// </summary>
public sealed class ReportData
{
    private readonly Dictionary<Guid, Property> _properties;
    private readonly ILookup<Guid, TariffComponent> _componentsByContract;
    private readonly ILookup<Guid, ContractMeter> _linksByContract;
    private readonly ILookup<Guid, Meter> _metersByProperty;
    private readonly ILookup<(Guid ContractId, YearMonth Period), Bill> _billsByContractPeriod;
    private readonly Dictionary<(Guid ContractId, YearMonth Period), CalculatedCharge?> _charges = [];

    /// <summary>Creates the data set.</summary>
    public ReportData(
        IReadOnlyList<Property> properties,
        IReadOnlyList<Category> categories,
        IReadOnlyList<Contract> contracts,
        IReadOnlyList<ContractMeter> contractMeters,
        IReadOnlyList<Meter> meters,
        IReadOnlyList<Bill> bills,
        IReadOnlyList<TariffComponent> tariffComponents,
        IReadOnlyCollection<VatRate> vatCatalog)
    {
        Properties = [.. properties.Where(p => !p.IsDeleted)];
        Categories = [.. categories.Where(c => !c.IsDeleted)];
        Contracts = [.. contracts.Where(c => !c.IsDeleted)];
        ContractMeters = [.. contractMeters.Where(c => !c.IsDeleted)];
        Meters = [.. meters.Where(m => !m.IsDeleted)];
        Bills = [.. bills.Where(b => !b.IsDeleted)];
        VatCatalog = vatCatalog;

        _properties = Properties.ToDictionary(p => p.Id);
        _componentsByContract = tariffComponents.Where(t => !t.IsDeleted).ToLookup(t => t.ContractId);
        _linksByContract = ContractMeters.ToLookup(c => c.ContractId);
        _metersByProperty = Meters.ToLookup(m => m.PropertyId);
        _billsByContractPeriod = Bills.Where(b => b.ContractId is not null).ToLookup(b => (b.ContractId!.Value, b.Period));
    }

    /// <summary>The properties in list order: active ones first, then archived ones.</summary>
    public IReadOnlyList<Property> Properties { get; }

    /// <summary>The categories.</summary>
    public IReadOnlyList<Category> Categories { get; }

    /// <summary>The contracts.</summary>
    public IReadOnlyList<Contract> Contracts { get; }

    /// <summary>The links between contracts and meters.</summary>
    public IReadOnlyList<ContractMeter> ContractMeters { get; }

    /// <summary>The meters with registers and readings.</summary>
    public IReadOnlyList<Meter> Meters { get; }

    /// <summary>The bills.</summary>
    public IReadOnlyList<Bill> Bills { get; }

    /// <summary>The VAT catalog.</summary>
    public IReadOnlyCollection<VatRate> VatCatalog { get; }

    /// <summary>The first month with a bill or a reading; null if there is neither.</summary>
    public YearMonth? EarliestMonth
    {
        get
        {
            var months = Bills.Select(b => b.Period)
                .Concat(Meters.SelectMany(m => m.Registers).Where(r => !r.IsDeleted).SelectMany(r => r.Readings)
                    .Where(r => !r.IsDeleted).Select(r => YearMonth.From(r.Date)))
                .ToList();
            return months.Count == 0 ? null : months.Min();
        }
    }

    /// <summary>The last month with a bill (bills of prepaid services may lie in the future); null if there is none.</summary>
    public YearMonth? LatestMonth => Bills.Count == 0 ? null : Bills.Max(b => b.Period);

    /// <summary>The meters of a property.</summary>
    public IEnumerable<Meter> MetersOf(Guid propertyId) => _metersByProperty[propertyId];

    /// <summary>
    /// The calculation of a contract for a month as it would be made now (tariffs and readings as they are today);
    /// null for contracts that are not billed by consumption (<see cref="BillingMode.Actual"/> only) or whose property is unknown.
    /// </summary>
    public CalculatedCharge? Charge(Contract contract, YearMonth period)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var key = (contract.Id, period);
        if (_charges.TryGetValue(key, out var known))
        {
            return known;
        }

        CalculatedCharge? result = null;
        if (contract.BillingMode == BillingMode.Actual && _properties.TryGetValue(contract.PropertyId, out var property))
        {
            var (quantities, amounts) = BillQuantitySelection.From(_billsByContractPeriod[key].OrderBy(b => b.CreatedAt));
            result = ChargeCalculator.Calculate(new ChargeInput(
                contract,
                property,
                [.. _componentsByContract[contract.Id].OrderBy(t => t.SortOrder).ThenBy(t => t.Name)],
                [.. _linksByContract[contract.Id].OrderBy(c => c.ValidFrom)],
                [.. _metersByProperty[contract.PropertyId].OrderBy(m => m.InstalledOn).ThenBy(m => m.Id)],
                VatCatalog,
                period,
                quantities,
                amounts));
        }

        _charges[key] = result;
        return result;
    }
}
