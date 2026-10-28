using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using static HomeUtilities.Core.Tests.Billing.BillingFixtures;

namespace HomeUtilities.Core.Tests.Billing;

public class DraftFactoryTests
{
    private static BillTemplate Template() => new()
    {
        Name = "Internet + TV",
        PropertyId = Guid.NewGuid(),
        CategoryId = Guid.NewGuid(),
        SupplierId = Guid.NewGuid(),
        Gross = 24.20m,
        VatPercent = 21m,
        Timing = BillingTiming.InAdvance,
        DueDayOffset = 14,
        Schedule = new Schedule { Kind = ScheduleKind.Monthly, StartMonth = new YearMonth(2026, 1), DayOfMonth = 5 },
    };

    [Fact]
    public void Create_CopiesTheTemplateIntoADraft()
    {
        var template = Template();
        var draft = DraftFactory.Create(template, new YearMonth(2026, 11), Clock);

        Assert.True(draft.IsDraft);
        Assert.Equal(template.Id, draft.TemplateId);
        Assert.Equal(template.PropertyId, draft.PropertyId);
        Assert.Equal(template.CategoryId, draft.CategoryId);
        Assert.Equal(template.SupplierId, draft.SupplierId);
        Assert.Equal(new YearMonth(2026, 11), draft.Period);
        Assert.Equal(BillKind.OneOff, draft.Kind);
        Assert.Equal("Internet + TV", draft.Title);
        // prepaid period 2026-11: issued 05.10.2026, due 19.10.2026
        Assert.Equal(new DateOnly(2026, 10, 5), draft.IssuedOn);
        Assert.Equal(new DateOnly(2026, 10, 19), draft.DueOn);
        // 24.20 incl. 21 % VAT = net 20.00 + VAT 4.20
        Assert.Equal(new AmountSet(20.00m, 21m, 4.20m, 24.20m), new AmountSet(draft.Net, draft.VatPercent, draft.Vat, draft.Gross));
    }

    [Fact]
    public void Create_WithoutAmount_HasTotalZero()
    {
        var template = Template();
        template.Gross = null;
        template.VatPercent = null;

        var draft = DraftFactory.Create(template, new YearMonth(2026, 11), Clock);

        Assert.Equal(0m, draft.Gross);
        Assert.Null(draft.Net);
    }

    [Fact]
    public void Create_WithContract_UsesItsCurrencyAndRegularKind()
    {
        var contract = Contract();
        contract.Currency = Currency.LVL;
        var template = Template();
        template.ContractId = contract.Id;
        template.Title = "Monthly fee";

        var draft = DraftFactory.Create(template, new YearMonth(2026, 11), Clock, contract);

        Assert.Equal(BillKind.Regular, draft.Kind);
        Assert.Equal(Currency.LVL, draft.Currency);
        Assert.Equal("Monthly fee", draft.Title);
    }

    [Fact]
    public void Create_CopiesLinesAndEmptyCustomFields()
    {
        var template = Template();
        template.Lines = [new BillTemplateLine { Description = "TV package", Gross = 10m }, new BillTemplateLine { Description = "Internet", Gross = 14.20m }];
        var billField = new CustomFieldDefinition { OwnerId = template.Id, Name = "Reference", Kind = CustomFieldKind.Text, Target = CustomFieldTarget.Bill };
        var lineField = new CustomFieldDefinition { OwnerId = template.Id, Name = "Line note", Kind = CustomFieldKind.Text, Target = CustomFieldTarget.BillLine };
        template.CustomFields = [billField, lineField];

        var draft = DraftFactory.Create(template, new YearMonth(2026, 11), Clock);

        Assert.Equal(["TV package", "Internet"], draft.Lines.Select(l => l.Description));
        Assert.All(draft.Lines, l => Assert.Equal(draft.Id, l.BillId));
        Assert.Equal([0, 1], draft.Lines.Select(l => l.SortOrder));
        var value = Assert.Single(draft.CustomFields);
        Assert.Equal(new CustomFieldValue(billField.Id, "Reference", CustomFieldKind.Text, string.Empty), value);
    }

    [Fact]
    public void Create_NotAsDraft_IsAnOrdinaryBill()
    {
        var bill = DraftFactory.Create(Template(), new YearMonth(2026, 11), Clock, isDraft: false);

        Assert.False(bill.IsDraft);
        Assert.NotNull(bill.TemplateId);
    }
}
