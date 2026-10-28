using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Data.Sample;

public sealed partial class SampleDataGenerator
{
    /// <summary>The sample internet bills of this many months ago (and of the current month) are drafts of the template.</summary>
    private const int DraftMonthsAgo = 1;

    private BillTemplate? _internetTemplate;

    /// <summary>
    /// Saves the "Internet + TV" template before the bills are made: its bills carry its id, and the two newest
    /// periods are left to the template, which creates them as drafts (see <see cref="CreateDraftsAsync"/>).
    /// </summary>
    private async Task AddInternetTemplateAsync(Property flat, Contract internet, Supplier supplier, CancellationToken ct)
    {
        var current = YearMonth.From(_today);
        _internetTemplate = await SaveAsync(
            new BillTemplate
            {
                Name = "Internet + TV",
                PropertyId = flat.Id,
                CategoryId = internet.CategoryId,
                ContractId = internet.Id,
                SupplierId = supplier.Id,
                Title = "Internet and TV",
                Gross = 22.95m,
                Timing = BillingTiming.InAdvance,
                DueDayOffset = 14,
                RemindDaysBefore = 3,
                Schedule = new Schedule { Kind = ScheduleKind.Monthly, StartMonth = current.AddMonths(-10), DayOfMonth = 5 },
                Notes = "Invented sample template: the fixed monthly fee, prepaid.",
            },
            billTemplates,
            ct);
    }

    /// <summary>Home insurance (yearly in March), chimney cleaning (yearly, amount entered each time) and a one-time purchase.</summary>
    private async Task AddOtherTemplatesAsync(Property flat, Property cottage, CancellationToken ct)
    {
        var current = YearMonth.From(_today);

        // The insurance of this March is already paid: the template only waits for the next March.
        var lastMarch = new YearMonth(current.Month >= 3 ? current.Year : current.Year - 1, 3);
        await SaveAsync(
            new BillTemplate
            {
                Name = "Home insurance",
                PropertyId = flat.Id,
                CategoryId = _categoryIds[ServiceKind.Insurance],
                Title = "Home insurance",
                Gross = 120m,
                DueDayOffset = 14,
                Schedule = new Schedule { Kind = ScheduleKind.Yearly, StartMonth = lastMarch.AddMonths(-24), DayOfMonth = 10 },
                LastGeneratedPeriod = lastMarch,
                Notes = "Invented sample template: yearly premium.",
            },
            billTemplates,
            ct);

        // The sweep came last month and the invoice is not in: a draft without an amount appears.
        await SaveAsync(
            new BillTemplate
            {
                Name = "Chimney cleaning",
                PropertyId = cottage.Id,
                CategoryId = _categoryIds[ServiceKind.Maintenance],
                Title = "Chimney cleaning",
                DueDayOffset = 14,
                Schedule = new Schedule { Kind = ScheduleKind.Yearly, StartMonth = current.AddMonths(-1), DayOfMonth = 15 },
                Notes = "Invented sample template: the amount is typed from each invoice.",
            },
            billTemplates,
            ct);

        await SaveAsync(
            new BillTemplate
            {
                Name = "Water meter replacement",
                PropertyId = flat.Id,
                CategoryId = _categoryIds[ServiceKind.Maintenance],
                Title = "Water meter replacement",
                Net = 28.10m,
                VatPercent = 21m,
                Schedule = new Schedule { Kind = ScheduleKind.Once },
                Notes = "Invented sample template: used by hand with New bill > From template.",
            },
            billTemplates,
            ct);
    }

    /// <summary>Creates the drafts that are due today (the same call the app makes when Overview or Bills open).</summary>
    private Task<int> CreateDraftsAsync(CancellationToken ct) => billTemplates.GenerateDraftsAsync(_today, ct);
}
