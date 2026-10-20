using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Billing;

/// <summary>Validation rules of <see cref="Bill"/> and its lines.</summary>
public static class BillValidator
{
    private const int MaxTextLength = 100;

    /// <summary>Validates a bill; deleted lines are skipped.</summary>
    /// <param name="bill">The bill.</param>
    /// <param name="contract">The bill's contract, if it has one; enables the consistency checks against it.</param>
    public static ValidationResult Validate(Bill bill, Contract? contract)
    {
        ArgumentNullException.ThrowIfNull(bill);

        var errors = new List<ValidationError>();
        if (bill.PropertyId == Guid.Empty)
        {
            errors.Add(new(ErrorCodes.BillPropertyRequired, nameof(Bill.PropertyId)));
        }

        if (bill.CategoryId == Guid.Empty)
        {
            errors.Add(new(ErrorCodes.BillCategoryRequired, nameof(Bill.CategoryId)));
        }

        if ((bill.ContractId is null) != (bill.Kind == BillKind.OneOff))
        {
            errors.Add(new(ErrorCodes.BillContractKindMismatch, nameof(Bill.ContractId)));
        }

        if (bill.Kind == BillKind.OneOff && string.IsNullOrWhiteSpace(bill.Title))
        {
            errors.Add(new(ErrorCodes.BillTitleRequired, nameof(Bill.Title)));
        }

        if (contract is not null)
        {
            if (bill.ContractId != contract.Id)
            {
                errors.Add(new(ErrorCodes.BillContractMismatch, nameof(Bill.ContractId)));
            }

            if (bill.PropertyId != contract.PropertyId)
            {
                errors.Add(new(ErrorCodes.BillPropertyMismatch, nameof(Bill.PropertyId)));
            }

            if (bill.Currency != contract.Currency)
            {
                errors.Add(new(ErrorCodes.BillCurrencyMismatch, nameof(Bill.Currency)));
            }
        }

        if (bill.ServiceFrom.HasValue != bill.ServiceTo.HasValue)
        {
            var missing = bill.ServiceFrom.HasValue ? nameof(Bill.ServiceTo) : nameof(Bill.ServiceFrom);
            errors.Add(new(ErrorCodes.BillServicePeriodIncomplete, missing));
        }
        else if (bill.ServiceFrom is { } from && bill.ServiceTo is { } to && to < from)
        {
            errors.Add(new(ErrorCodes.BillServiceToBeforeFrom, nameof(Bill.ServiceTo)));
        }

        if (bill.IssuedOn is { } issued && bill.DueOn is { } due && due < issued)
        {
            errors.Add(new(ErrorCodes.BillDueBeforeIssued, nameof(Bill.DueOn)));
        }

        CheckAmounts(errors, bill.Net, bill.VatPercent, bill.Vat, bill.Gross);
        if (bill.Gross < 0 && bill.Kind is not (BillKind.Settlement or BillKind.Correction))
        {
            errors.Add(new(ErrorCodes.BillNegativeNotAllowed, nameof(Bill.Gross)));
        }

        CheckLength(errors, bill.Title, nameof(Bill.Title), ErrorCodes.BillTitleTooLong);
        CheckLength(errors, bill.Number, nameof(Bill.Number), ErrorCodes.BillNumberTooLong);
        AddCustomFields(errors, bill.CustomFields);
        ValidationHelpers.CheckNotes(errors, bill.Notes, nameof(Bill.Notes));

        for (var i = 0; i < bill.Lines.Count; i++)
        {
            if (!bill.Lines[i].IsDeleted)
            {
                errors.AddRange(ValidationHelpers.Prefix(nameof(Bill.Lines), i, ValidateLine(bill.Lines[i])));
            }
        }

        return ValidationHelpers.ToResult(errors);
    }

    private static ValidationResult ValidateLine(BillLine line)
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(line.Description))
        {
            errors.Add(new(ErrorCodes.BillLineDescriptionRequired, nameof(BillLine.Description)));
        }

        CheckAmounts(errors, line.Net, line.VatPercent, line.Vat, line.Gross);
        AddCustomFields(errors, line.CustomFields);
        ValidationHelpers.CheckNotes(errors, line.Note, nameof(BillLine.Note));
        return ValidationHelpers.ToResult(errors);
    }

    private static void CheckAmounts(
        List<ValidationError> errors, decimal? net, decimal? vatPercent, decimal? vat, decimal gross)
    {
        if (net is null && vatPercent is null && vat is null)
        {
            return;
        }

        var (_, error) = Amounts.Complete(net, vatPercent, gross);
        if (error is not null)
        {
            errors.Add(error);
        }
    }

    private static void CheckLength(List<ValidationError> errors, string? text, string field, string code)
    {
        if (text is { Length: > MaxTextLength })
        {
            errors.Add(new(code, field, [MaxTextLength]));
        }
    }

    private static void AddCustomFields(List<ValidationError> errors, List<CustomFieldValue> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            errors.AddRange(ValidationHelpers.Prefix(nameof(Bill.CustomFields), i, CustomFieldValidator.Validate(values[i])));
        }
    }
}
