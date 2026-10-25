using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tests.Billing;

public class BillDefaultsTests
{
    [Fact]
    public void DueDate_InArrears_IsTheDueDayOfTheMonthAfterThePeriod()
    {
        var contract = new Contract { PaymentDueDay = 20 };

        Assert.Equal(new DateOnly(2026, 11, 20), BillDefaults.DueDate(contract, new YearMonth(2026, 10)));
    }

    [Fact]
    public void DueDate_InArrears_DecemberPeriodDueInJanuary()
    {
        var contract = new Contract { PaymentDueDay = 5 };

        Assert.Equal(new DateOnly(2027, 1, 5), BillDefaults.DueDate(contract, new YearMonth(2026, 12)));
    }

    [Fact]
    public void DueDate_InAdvance_IsInTheMonthBeforeThePeriod()
    {
        var contract = new Contract { PaymentDueDay = 15, BillingTiming = BillingTiming.InAdvance };

        Assert.Equal(new DateOnly(2026, 10, 15), BillDefaults.DueDate(contract, new YearMonth(2026, 11)));
    }

    [Fact]
    public void DueDate_DayBeyondTheMonthLength_BecomesTheLastDay()
    {
        var contract = new Contract { PaymentDueDay = 31 };

        // period 2027-01 -> due in February 2027 (28 days)
        Assert.Equal(new DateOnly(2027, 2, 28), BillDefaults.DueDate(contract, new YearMonth(2027, 1)));
    }

    [Fact]
    public void DueDate_WithoutDueDay_IsNull()
    {
        Assert.Null(BillDefaults.DueDate(new Contract(), new YearMonth(2026, 10)));
    }
}
