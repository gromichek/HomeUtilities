using HomeUtilities.Core.Billing;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Store of payments with their allocations.</summary>
public interface IPaymentStore : IStore<Payment>
{
    /// <summary>
    /// Payments of the contract: those with that <see cref="Payment.ContractId"/> and those with an allocation
    /// to one of its bills. Ordered by paid-on date.
    /// </summary>
    Task<IReadOnlyList<Payment>> ListByContractAsync(Guid contractId, CancellationToken ct = default);

    /// <summary>Payments with a non-deleted allocation to the bill, ordered by paid-on date.</summary>
    Task<IReadOnlyList<Payment>> ListByBillAsync(Guid billId, CancellationToken ct = default);

    /// <summary>
    /// Payments of the property: those with that <see cref="Payment.PropertyId"/>, those of its contracts and
    /// those allocated to its bills. Ordered by paid-on date.
    /// </summary>
    Task<IReadOnlyList<Payment>> ListByPropertyAsync(Guid propertyId, CancellationToken ct = default);
}
