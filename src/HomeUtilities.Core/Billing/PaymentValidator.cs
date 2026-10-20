using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Billing;

/// <summary>Validation rules of <see cref="Payment"/> and its allocations.</summary>
public static class PaymentValidator
{
    /// <summary>Validates a payment; deleted allocations are skipped.</summary>
    /// <param name="payment">The payment.</param>
    /// <param name="bills">Bills by id, used to look up the allocated bills.</param>
    public static ValidationResult Validate(Payment payment, IReadOnlyDictionary<Guid, Bill> bills)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(bills);

        var errors = new List<ValidationError>();
        if (payment.Amount == 0)
        {
            errors.Add(new(ErrorCodes.PaymentAmountZero, nameof(Payment.Amount)));
        }

        var seen = new HashSet<Guid>();
        var total = 0m;
        for (var i = 0; i < payment.Allocations.Count; i++)
        {
            var allocation = payment.Allocations[i];
            if (allocation.IsDeleted)
            {
                continue;
            }

            var field = $"{nameof(Payment.Allocations)}[{i}].";
            if (allocation.Amount == 0)
            {
                errors.Add(new(ErrorCodes.PaymentAllocationAmountZero, field + nameof(PaymentAllocation.Amount)));
            }
            else if (payment.Amount != 0 && Math.Sign(allocation.Amount) != Math.Sign(payment.Amount))
            {
                errors.Add(new(ErrorCodes.PaymentAllocationSignMismatch, field + nameof(PaymentAllocation.Amount)));
            }

            total += Math.Abs(allocation.Amount);

            if (!bills.TryGetValue(allocation.BillId, out var bill) || bill.IsDeleted)
            {
                errors.Add(new(ErrorCodes.PaymentAllocationBillUnknown, field + nameof(PaymentAllocation.BillId)));
            }
            else if (bill.Currency != payment.Currency)
            {
                errors.Add(new(ErrorCodes.PaymentAllocationCurrencyMismatch, field + nameof(PaymentAllocation.BillId)));
            }

            if (!seen.Add(allocation.BillId))
            {
                errors.Add(new(ErrorCodes.PaymentAllocationDuplicateBill, field + nameof(PaymentAllocation.BillId)));
            }

            ValidationHelpers.CheckNotes(errors, allocation.Note, nameof(PaymentAllocation.Note), field);
        }

        if (total > Math.Abs(payment.Amount))
        {
            errors.Add(new(ErrorCodes.PaymentOverAllocated, nameof(Payment.Allocations)));
        }

        ValidationHelpers.CheckNotes(errors, payment.Notes, nameof(Payment.Notes));
        return ValidationHelpers.ToResult(errors);
    }
}
