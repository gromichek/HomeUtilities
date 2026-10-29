using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Equalized;
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
    private readonly ILookup<Guid, ContractMeter> _linksByMeter;
    private readonly ILookup<(Guid ContractId, YearMonth Period), Bill> _billsByContractPeriod;
    private readonly ILookup<Guid, EqualizedPlan> _plansByContract;
    private readonly Dictionary<(Guid ContractId, YearMonth Period), CalculatedCharge?> _charges = [];
    private readonly Dictionary<Guid, IReadOnlyList<TariffComponent>> _sortedComponents = [];
    private readonly Dictionary<Guid, IReadOnlyList<ContractMeter>> _sortedLinks = [];
    private readonly Dictionary<Guid, IReadOnlyList<Meter>> _sortedMeters = [];

    /// <summary>Creates the data set.</summary>
    public ReportData(
        IReadOnlyList<Property> properties,
        IReadOnlyList<Category> categories,
        IReadOnlyList<Contract> contracts,
        IReadOnlyList<ContractMeter> contractMeters,
        IReadOnlyList<Meter> meters,
        IReadOnlyList<Bill> bills,
        IReadOnlyList<TariffComponent> tariffComponents,
        IReadOnlyCollection<VatRate> vatCatalog,
        IReadOnlyList<EqualizedPlan>? equalizedPlans = null)
    {
        Properties = [.. properties.Where(p => !p.IsDeleted)];
        Categories = [.. categories.Where(c => !c.IsDeleted)];
        Contracts = [.. contracts.Where(c => !c.IsDeleted)];
        ContractMeters = [.. contractMeters.Where(c => !c.IsDeleted)];
        Meters = [.. meters.Where(m => !m.IsDeleted)];
        Bills = [.. bills.Where(b => !b.IsDeleted)];
        VatCatalog = vatCatalog;

        _properties = Properties.ToDictionary(p => p.Id);
        _plansByContract = (equalizedPlans ?? []).Where(p => !p.IsDeleted).ToLookup(p => p.ContractId);
        _componentsByContract = tariffComponents.Where(t => !t.IsDeleted).ToLookup(t => t.ContractId);
        _linksByContract = ContractMeters.ToLookup(c => c.ContractId);
        _metersByProperty = Meters.ToLookup(m => m.PropertyId);
        _linksByMeter = ContractMeters.ToLookup(c => c.MeterId);
        ContractById = Contracts.ToDictionary(c => c.Id);
        _billsByContractPeriod = Bills.Where(b => b.ContractId is not null).ToLookup(b => (b.ContractId!.Value, b.Period));
    }

    /// <summary>The properties in list order: active ones first, then archived ones.</summary>
    public IReadOnlyList<Property> Properties { get; }

    /// <summary>The categories.</summary>
    public IReadOnlyList<Category> Categories { get; }

    /// <summary>The contracts.</summary>
    public IReadOnlyList<Contract> Contracts { get; }

    /// <summary>The contracts by id (built once, so reports do not search the list).</summary>
    internal IReadOnlyDictionary<Guid, Contract> ContractById { get; }

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

    /// <summary>
    /// True if an equalized plan of the contract covers the month: such a month is billed by the plan, not by consumption.
    /// Months before the first plan behave like <see cref="BillingMode.Actual"/>.
    /// </summary>
    public bool IsEqualized(Contract contract, YearMonth period)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return EqualizedPlans.AmountFor(_plansByContract[contract.Id], period) is not null;
    }

    /// <summary>True if the contract can have months billed by consumption: its mode is Actual, or Equalized (earlier months).</summary>
    internal static bool CanBeBilledByConsumption(Contract contract) =>
        contract.BillingMode is BillingMode.Actual or BillingMode.Equalized;

    /// <summary>The links of a contract to meters (not deleted), in no particular order.</summary>
    internal IEnumerable<ContractMeter> LinksOfContract(Guid contractId) => _linksByContract[contractId];

    /// <summary>The links of a meter to contracts (not deleted), in no particular order.</summary>
    internal IEnumerable<ContractMeter> LinksOfMeter(Guid meterId) => _linksByMeter[meterId];

    /// <summary>The meters of a property.</summary>
    public IEnumerable<Meter> MetersOf(Guid propertyId) => _metersByProperty[propertyId];

    /// <summary>
    /// The calculation of a contract for a month as it would be made now (tariffs and readings as they are today);
    /// null for months billed by an equalized plan, for contracts that are not billed by consumption (<see cref="BillingMode.Actual"/>,
    /// or <see cref="BillingMode.Equalized"/> for the months before its plans) or whose property is unknown.
    /// </summary>
    public CalculatedCharge? Charge(Contract contract, YearMonth period)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return IsEqualized(contract, period) ? null : ActualCharge(contract, period);
    }

    /// <summary>
    /// What the month really costs by consumption: readings times tariff, also for a month an equalized plan covers (the plan is only
    /// an advance, the settlement corrects it). Null for contracts that are not billed by consumption or whose property is unknown.
    /// </summary>
    public CalculatedCharge? ActualCharge(Contract contract, YearMonth period)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var key = (contract.Id, period);
        if (_charges.TryGetValue(key, out var known))
        {
            return known;
        }

        CalculatedCharge? result = null;
        if (CanBeBilledByConsumption(contract) && _properties.TryGetValue(contract.PropertyId, out var property))
        {
            var (quantities, amounts) = BillQuantitySelection.From(_billsByContractPeriod[key].OrderBy(b => b.CreatedAt));
            result = ChargeCalculator.Calculate(new ChargeInput(
                contract,
                property,
                Cached(_sortedComponents, contract.Id, () => [.. _componentsByContract[contract.Id].OrderBy(t => t.SortOrder).ThenBy(t => t.Name)]),
                Cached(_sortedLinks, contract.Id, () => [.. _linksByContract[contract.Id].OrderBy(c => c.ValidFrom)]),
                Cached(_sortedMeters, contract.PropertyId, () => [.. _metersByProperty[contract.PropertyId].OrderBy(m => m.InstalledOn).ThenBy(m => m.Id)]),
                VatCatalog,
                period,
                quantities,
                amounts));
        }

        _charges[key] = result;
        return result;
    }

    /// <summary>
    /// True if the contract has tariff components or linked meters: only then can a quantity be known for it. Reports skip the others
    /// without calculating every month.
    /// </summary>
    internal bool CanHaveQuantity(Contract contract) => _componentsByContract[contract.Id].Any() || _linksByContract[contract.Id].Any();

    /// <summary>The monthly amount of the equalized plan that covers the month; null when no plan does.</summary>
    internal decimal? PlanAmount(Contract contract, YearMonth period) => EqualizedPlans.AmountFor(_plansByContract[contract.Id], period);

    // The sorted lists of a contract do not change between its months: sort them once, not once per month.
    private static IReadOnlyList<T> Cached<T>(Dictionary<Guid, IReadOnlyList<T>> cache, Guid key, Func<IReadOnlyList<T>> make)
    {
        if (!cache.TryGetValue(key, out var list))
        {
            list = make();
            cache[key] = list;
        }

        return list;
    }
}
