using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.Core.Charges;

/// <summary>One line of a calculated charge. A tiered component yields one line per price slice.</summary>
/// <param name="ComponentId">The tariff component.</param>
/// <param name="ComponentName">Name of the component.</param>
/// <param name="ChargeKind">Service the line counts toward.</param>
/// <param name="CategoryId">Category of the line: the component's, else the contract's.</param>
/// <param name="Basis">Basis of the component.</param>
/// <param name="Quantity">Charged quantity (negative for net export); null if it is not known or not applicable.</param>
/// <param name="UnitPrice">Unit price as given by the rate; null if not known or not applicable.</param>
/// <param name="Net">Net amount, rounded to 2 decimals.</param>
/// <param name="VatPercent">VAT percent that was applied.</param>
/// <param name="Vat">VAT amount, rounded to 2 decimals.</param>
/// <param name="Gross">Gross amount.</param>
/// <param name="IsFromBill">True if the amount comes from the supplier bill instead of a calculation.</param>
/// <param name="IsComplete">False if the line could not be calculated completely.</param>
/// <param name="Consumption">Consumption results behind the quantity; empty for bases without readings.</param>
/// <param name="PriceIncludesVat">True if <paramref name="UnitPrice"/> is a gross price (the rate includes VAT).</param>
/// <param name="AmountFromBill">True if the amount of the line was typed from the bill instead of being quantity × price.</param>
public sealed record ChargeLine(
    Guid ComponentId,
    string ComponentName,
    ServiceKind ChargeKind,
    Guid CategoryId,
    ChargeBasis Basis,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal Net,
    decimal VatPercent,
    decimal Vat,
    decimal Gross,
    bool IsFromBill,
    bool IsComplete,
    IReadOnlyList<ConsumptionResult> Consumption,
    bool PriceIncludesVat = false,
    bool AmountFromBill = false);
