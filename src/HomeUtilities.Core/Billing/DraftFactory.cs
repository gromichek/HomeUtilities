using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Billing;

/// <summary>Creates bills from a bill template.</summary>
public static class DraftFactory
{
    /// <summary>
    /// Creates the draft bill of a period: the template's property, category, contract, supplier, title, amounts and lines,
    /// empty values for its custom fields, issue and due dates from the schedule. With no amount in the template the total
    /// is 0 ("amount to enter").
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="period">The period (month) the bill belongs to.</param>
    /// <param name="clock">Clock for the timestamps.</param>
    /// <param name="contract">The template's contract, if any; its currency is used.</param>
    /// <param name="isDraft">False creates an ordinary bill ("New bill from template").</param>
    public static Bill Create(BillTemplate template, YearMonth period, IClock clock, Contract? contract = null, bool isDraft = true)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(clock);

        var bill = new Bill
        {
            PropertyId = template.PropertyId,
            CategoryId = template.CategoryId,
            ContractId = template.ContractId,
            SupplierId = template.SupplierId,
            Period = period,
            Kind = template.ContractId is null ? BillKind.OneOff : BillKind.Regular,
            Title = string.IsNullOrWhiteSpace(template.Title) ? template.Name : template.Title,
            IssuedOn = BillTemplateScheduler.IssueDate(template, period),
            DueOn = BillTemplateScheduler.DueDate(template, period),
            Currency = contract?.Currency ?? Currency.EUR,
            TemplateId = template.Id,
            IsDraft = isDraft,
        };

        var (amount, error) = Amounts.Complete(template.Net, template.VatPercent, template.Gross);
        if (error is null && amount is not null)
        {
            bill.Net = amount.Net;
            bill.VatPercent = amount.VatPercent;
            bill.Vat = amount.Vat;
            bill.Gross = amount.Gross;
        }

        bill.Lines =
        [
            .. template.Lines.Select((line, index) => line.ToBillLine(bill.Id, index))
        ];
        foreach (var line in bill.Lines)
        {
            line.Touch(clock);
        }

        bill.CustomFields =
        [
            .. template.CustomFields
                .Where(d => !d.IsDeleted && d.Target == CustomFieldTarget.Bill)
                .OrderBy(d => d.SortOrder)
                .Select(d => new CustomFieldValue(d.Id, d.Name, d.Kind, string.Empty)),
        ];
        bill.Touch(clock);
        return bill;
    }
}
