using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Charges;

/// <summary>The expected charge of a contract for a period.</summary>
/// <param name="ContractId">The contract.</param>
/// <param name="Period">The billing month.</param>
/// <param name="Currency">Currency of all amounts.</param>
/// <param name="Lines">Charge lines ordered by component.</param>
/// <param name="TotalNet">Sum of the net amounts of the lines.</param>
/// <param name="TotalVat">Sum of the VAT amounts of the lines.</param>
/// <param name="TotalGross">Sum of the gross amounts of the lines.</param>
/// <param name="IsComplete">True if every line could be calculated completely.</param>
/// <param name="Warnings">Findings the user should look at.</param>
public sealed record CalculatedCharge(
    Guid ContractId,
    YearMonth Period,
    Currency Currency,
    IReadOnlyList<ChargeLine> Lines,
    decimal TotalNet,
    decimal TotalVat,
    decimal TotalGross,
    bool IsComplete,
    IReadOnlyList<ChargeWarning> Warnings);
