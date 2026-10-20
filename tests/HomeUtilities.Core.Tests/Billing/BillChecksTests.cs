using HomeUtilities.Core.Billing;
using static HomeUtilities.Core.Tests.Billing.BillingFixtures;

namespace HomeUtilities.Core.Tests.Billing;

public class BillChecksTests
{
    private static Bill WithLines(decimal gross, params decimal[] lines)
    {
        var bill = BillFor(Contract(), gross);
        bill.Lines = [.. lines.Select(g => new BillLine { Description = "L", Gross = g })];
        return bill;
    }

    [Fact]
    public void Matching_lines_give_no_warning() =>
        Assert.Empty(BillChecks.Warnings(WithLines(55.25m, 34.32m, 20.93m))); // 34.32 + 20.93 = 55.25

    [Fact]
    public void Mismatching_lines_warn() =>
        Assert.Equal(BillWarningCodes.LinesTotalMismatch, Assert.Single(BillChecks.Warnings(WithLines(59.49m, 34.32m, 20.93m))).Code); // 55.25 vs 59.49

    [Fact]
    public void Difference_of_one_cent_is_tolerated() =>
        Assert.Empty(BillChecks.Warnings(WithLines(55.26m, 34.32m, 20.93m)));

    [Fact]
    public void Bill_without_lines_has_no_warning() =>
        Assert.Empty(BillChecks.Warnings(BillFor(Contract(), 10m)));

    [Fact]
    public void Deleted_lines_are_ignored()
    {
        var bill = WithLines(55.25m, 34.32m, 20.93m, 99m);
        bill.Lines[2].IsDeleted = true;
        Assert.Empty(BillChecks.Warnings(bill));
    }
}
