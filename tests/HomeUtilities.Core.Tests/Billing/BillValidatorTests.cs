using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using static HomeUtilities.Core.Tests.Billing.BillingFixtures;

namespace HomeUtilities.Core.Tests.Billing;

public class BillValidatorTests
{
    private static (Contract Contract, Bill Bill) Valid()
    {
        var contract = Contract();
        var bill = BillFor(contract, 55.25m);
        return (contract, bill);
    }

    private static string[] Codes(Bill bill, Contract? contract) =>
        [.. BillValidator.Validate(bill, contract).Errors.Select(e => e.Code)];

    [Fact]
    public void Valid_bill_passes()
    {
        var (contract, bill) = Valid();
        Assert.True(BillValidator.Validate(bill, contract).IsValid);
    }

    [Fact]
    public void Valid_one_off_passes()
    {
        var bill = Bill.CreateOneOff(Guid.CreateVersion7(), Guid.CreateVersion7(), "Repair", new YearMonth(2026, 10), Clock);
        bill.Gross = 80m;
        Assert.True(BillValidator.Validate(bill, null).IsValid);
    }

    [Fact]
    public void Property_and_category_are_required()
    {
        var (contract, bill) = Valid();
        bill.PropertyId = Guid.Empty;
        bill.CategoryId = Guid.Empty;
        Assert.Equal([ErrorCodes.BillPropertyRequired, ErrorCodes.BillCategoryRequired, ErrorCodes.BillPropertyMismatch], Codes(bill, contract));
    }

    [Fact]
    public void One_off_without_title_is_rejected()
    {
        var bill = Bill.CreateOneOff(Guid.CreateVersion7(), Guid.CreateVersion7(), " ", new YearMonth(2026, 10), Clock);
        Assert.Equal([ErrorCodes.BillTitleRequired], Codes(bill, null));
    }

    [Fact]
    public void Contract_must_match_kind()
    {
        var (_, bill) = Valid();
        bill.Kind = BillKind.OneOff;
        bill.Title = "x";
        Assert.Equal([ErrorCodes.BillContractKindMismatch], Codes(bill, null));

        var (_, regular) = Valid();
        regular.ContractId = null;
        Assert.Equal([ErrorCodes.BillContractKindMismatch], Codes(regular, null));
    }

    [Fact]
    public void Contract_property_and_currency_must_match()
    {
        var (contract, bill) = Valid();
        bill.ContractId = Guid.CreateVersion7();
        Assert.Equal([ErrorCodes.BillContractMismatch], Codes(bill, contract));

        (contract, bill) = Valid();
        bill.PropertyId = Guid.CreateVersion7();
        Assert.Equal([ErrorCodes.BillPropertyMismatch], Codes(bill, contract));

        (contract, bill) = Valid();
        bill.Currency = Currency.LVL;
        Assert.Equal([ErrorCodes.BillCurrencyMismatch], Codes(bill, contract));
    }

    [Fact]
    public void Service_period_needs_both_dates_in_order()
    {
        var (contract, bill) = Valid();
        bill.ServiceFrom = new DateOnly(2026, 10, 1);
        Assert.Equal([ErrorCodes.BillServicePeriodIncomplete], Codes(bill, contract));

        bill.ServiceFrom = null;
        bill.ServiceTo = new DateOnly(2026, 10, 31);
        Assert.Equal([ErrorCodes.BillServicePeriodIncomplete], Codes(bill, contract));

        bill.ServiceFrom = new DateOnly(2026, 11, 1);
        Assert.Equal([ErrorCodes.BillServiceToBeforeFrom], Codes(bill, contract));

        bill.ServiceFrom = new DateOnly(2026, 10, 31);
        Assert.Empty(Codes(bill, contract));
    }

    [Fact]
    public void Due_date_must_not_precede_issue_date()
    {
        var (contract, bill) = Valid();
        bill.IssuedOn = new DateOnly(2026, 10, 10);
        bill.DueOn = new DateOnly(2026, 10, 9);
        Assert.Equal([ErrorCodes.BillDueBeforeIssued], Codes(bill, contract));

        bill.DueOn = new DateOnly(2026, 10, 10);
        Assert.Empty(Codes(bill, contract));
    }

    [Fact]
    public void Inconsistent_amounts_are_rejected_with_the_amount_code()
    {
        var (contract, bill) = Valid();
        bill.Net = 49.17m;
        bill.VatPercent = 21m;
        bill.Gross = 60m;
        Assert.Equal([ErrorCodes.AmountInconsistent], Codes(bill, contract));

        bill.Gross = 59.49m;
        Assert.Empty(Codes(bill, contract));
    }

    [Fact]
    public void Negative_total_only_for_settlement_and_correction()
    {
        var (contract, bill) = Valid();
        bill.Gross = -10m;
        Assert.Equal([ErrorCodes.BillNegativeNotAllowed], Codes(bill, contract));

        bill.Kind = BillKind.Settlement;
        Assert.Empty(Codes(bill, contract));
        bill.Kind = BillKind.Correction;
        Assert.Empty(Codes(bill, contract));
    }

    [Fact]
    public void Title_number_and_notes_are_limited()
    {
        var (contract, bill) = Valid();
        bill.Title = new string('t', 101);
        bill.Number = new string('n', 101);
        bill.Notes = new string('x', 2001);
        Assert.Equal([ErrorCodes.BillTitleTooLong, ErrorCodes.BillNumberTooLong, ErrorCodes.NotesTooLong], Codes(bill, contract));

        bill.Title = new string('t', 100);
        bill.Number = new string('n', 100);
        bill.Notes = new string('x', 2000);
        Assert.Empty(Codes(bill, contract));
    }

    [Fact]
    public void Invalid_custom_field_value_is_reported_with_index()
    {
        var (contract, bill) = Valid();
        bill.CustomFields = [new(null, "Losses", CustomFieldKind.Number, "abc")];
        var error = Assert.Single(BillValidator.Validate(bill, contract).Errors);
        Assert.Equal((ErrorCodes.CustomFieldInvalidValue, "CustomFields[0].Value"), (error.Code, error.Field));
    }

    [Fact]
    public void Bad_line_is_reported_with_index_and_deleted_lines_are_skipped()
    {
        var (contract, bill) = Valid();
        var bad = new BillLine { Description = " ", Net = 10m, VatPercent = 21m, Gross = 20m, Note = new string('n', 2001) };
        var deleted = new BillLine { Description = string.Empty, IsDeleted = true };
        bill.Lines = [new BillLine { Description = "Water", Gross = 10m }, bad, deleted];

        var errors = BillValidator.Validate(bill, contract).Errors;

        Assert.Equal(
            [("bill_line.description_required", "Lines[1].Description"), ("amount.inconsistent", "Lines[1].Gross"), ("common.notes_too_long", "Lines[1].Note")],
            errors.Select(e => (e.Code, e.Field!)).ToArray());
    }
}
