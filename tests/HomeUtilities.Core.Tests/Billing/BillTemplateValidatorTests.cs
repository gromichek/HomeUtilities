using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Tests.Billing;

public class BillTemplateValidatorTests
{
    private static BillTemplate Valid() => new()
    {
        Name = "Insurance",
        PropertyId = Guid.NewGuid(),
        CategoryId = Guid.NewGuid(),
        Gross = 120m,
        Schedule = new Schedule { Kind = ScheduleKind.Yearly, StartMonth = new YearMonth(2026, 3), DayOfMonth = 15 },
    };

    private static string[] Codes(BillTemplate template) => [.. BillTemplateValidator.Validate(template).Errors.Select(e => e.Code)];

    [Fact]
    public void ValidTemplate_HasNoErrors() => Assert.True(BillTemplateValidator.Validate(Valid()).IsValid);

    [Fact]
    public void EmptyAmounts_AreAllowed()
    {
        var template = Valid();
        template.Gross = null;

        Assert.True(BillTemplateValidator.Validate(template).IsValid);
    }

    [Fact]
    public void NameAndReferences_AreRequired()
    {
        var template = new BillTemplate { Schedule = new Schedule() };

        Assert.Equal(
            [ErrorCodes.BillTemplateNameRequired, ErrorCodes.BillTemplatePropertyRequired, ErrorCodes.BillTemplateCategoryRequired],
            Codes(template));
    }

    [Fact]
    public void NameAndTitle_HaveALimit()
    {
        var template = Valid();
        template.Name = new string('x', 101);
        template.Title = new string('y', 101);

        Assert.Equal([ErrorCodes.BillTemplateNameTooLong, ErrorCodes.BillTemplateTitleTooLong], Codes(template));
    }

    [Fact]
    public void NetWithoutVatPercentOrGross_IsRejected()
    {
        var template = Valid();
        template.Gross = null;
        template.Net = 100m;

        Assert.Equal([ErrorCodes.AmountGrossRequired], Codes(template));
    }

    [Fact]
    public void InconsistentAmounts_AreRejected()
    {
        var template = Valid();
        template.Net = 100m;
        template.VatPercent = 21m;
        template.Gross = 130m;

        Assert.Equal([ErrorCodes.AmountInconsistent], Codes(template));
    }

    [Fact]
    public void Once_HasNoPeriodFields()
    {
        var template = Valid();
        template.Schedule = new Schedule { Kind = ScheduleKind.Once };
        Assert.True(BillTemplateValidator.Validate(template).IsValid);

        template.Schedule.StartMonth = new YearMonth(2026, 1);
        Assert.Equal([ErrorCodes.ScheduleOnceHasPeriod], Codes(template));
    }

    [Fact]
    public void ScheduledKinds_NeedAStartMonth()
    {
        var template = Valid();
        template.Schedule.StartMonth = null;

        Assert.Equal([ErrorCodes.ScheduleStartRequired], Codes(template));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(24, true)]
    [InlineData(25, false)]
    [InlineData(null, false)]
    public void EveryNMonths_NIsBetween2And24(int? n, bool valid)
    {
        var template = Valid();
        template.Schedule = new Schedule { Kind = ScheduleKind.EveryNMonths, EveryMonths = n, StartMonth = new YearMonth(2026, 1) };

        Assert.Equal(valid, BillTemplateValidator.Validate(template).IsValid);
    }

    [Fact]
    public void EveryMonths_OnlyForEveryNMonths()
    {
        var template = Valid();
        template.Schedule.EveryMonths = 3;

        Assert.Equal([ErrorCodes.ScheduleEveryMonthsNotAllowed], Codes(template));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(31, true)]
    [InlineData(32, false)]
    public void DayOfMonth_IsBetween1And31(int day, bool valid)
    {
        var template = Valid();
        template.Schedule.DayOfMonth = day;

        Assert.Equal(valid, BillTemplateValidator.Validate(template).IsValid);
    }

    [Fact]
    public void EndMonth_MustNotBeBeforeStart()
    {
        var template = Valid();
        template.Schedule.EndMonth = new YearMonth(2026, 2);
        Assert.Equal([ErrorCodes.ScheduleEndBeforeStart], Codes(template));

        template.Schedule.EndMonth = new YearMonth(2026, 3);
        Assert.True(BillTemplateValidator.Validate(template).IsValid);
    }

    [Fact]
    public void Days_AreBetween0And365()
    {
        var template = Valid();
        template.DueDayOffset = 366;
        template.RemindDaysBefore = -1;

        Assert.Equal([ErrorCodes.BillTemplateDaysOutOfRange], Codes(template));
        template.DueDayOffset = 14;
        Assert.Equal([ErrorCodes.BillTemplateDaysOutOfRange], Codes(template));
        template.RemindDaysBefore = 3;
        Assert.True(BillTemplateValidator.Validate(template).IsValid);
    }

    [Fact]
    public void Lines_NeedADescription()
    {
        var template = Valid();
        template.Lines = [new BillTemplateLine { Description = " ", Gross = 1m }];

        var error = Assert.Single(BillTemplateValidator.Validate(template).Errors);
        Assert.Equal(ErrorCodes.BillLineDescriptionRequired, error.Code);
        Assert.Equal("Lines[0].Description", error.Field);
    }

    [Fact]
    public void CustomFieldNames_MustBeUnique()
    {
        var template = Valid();
        template.CustomFields =
        [
            new CustomFieldDefinition { OwnerId = template.Id, Name = "Ref" },
            new CustomFieldDefinition { OwnerId = template.Id, Name = "ref" },
        ];

        Assert.Equal([ErrorCodes.CustomFieldNameDuplicate], Codes(template));
    }
}
