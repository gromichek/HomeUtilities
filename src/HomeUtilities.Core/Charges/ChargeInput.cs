using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.Core.Charges;

/// <summary>Everything <see cref="ChargeCalculator"/> needs to calculate one contract for one period.</summary>
/// <param name="Contract">The contract.</param>
/// <param name="Property">The property of the contract, for its parameters.</param>
/// <param name="Components">Tariff components of the contract.</param>
/// <param name="ContractMeters">Meter links of the contract.</param>
/// <param name="PropertyMeters">All meters of the property, needed to build meter chains.</param>
/// <param name="VatCatalog">The VAT catalog.</param>
/// <param name="Period">The billing month.</param>
/// <param name="QuantitiesFromBill">Quantities typed from the bill, by component id; null or missing = not known.</param>
public sealed record ChargeInput(
    Contract Contract,
    Property Property,
    IReadOnlyList<TariffComponent> Components,
    IReadOnlyList<ContractMeter> ContractMeters,
    IReadOnlyList<Meter> PropertyMeters,
    IReadOnlyCollection<VatRate> VatCatalog,
    YearMonth Period,
    IReadOnlyDictionary<Guid, decimal>? QuantitiesFromBill = null);
