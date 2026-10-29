using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Overview;

namespace HomeUtilities.Core.Reminders;

/// <summary>
/// Plans the notifications for the next days from the same data and rules as the Overview to-do. Pure logic: the platform only
/// hands the plan to the system. The plan is recomputed whenever the data or the settings change, so it never needs a
/// background job.
/// </summary>
public static class ReminderPlanner
{
    /// <summary>Default look-ahead in days.</summary>
    public const int DefaultHorizonDays = 45;

    /// <summary>At most this many reminders are planned (the system limits pending notifications).</summary>
    public const int MaxReminders = 60;

    /// <summary>Verification is announced this many days ahead (and on the due date).</summary>
    public const int VerificationLeadDays = MeterVerification.WarningDays;

    /// <summary>Draft reminders are at most this many days apart.</summary>
    public const int DraftIntervalDays = 3;

    /// <summary>At most this many draft reminders are planned, so an ignored draft does not nag for weeks.</summary>
    public const int MaxDraftReminders = 5;

    /// <summary>No reminder is shown from this time of day ...</summary>
    public static readonly TimeOnly QuietFrom = new(21, 0);

    /// <summary>... until this time of day; a reminder in between moves to this time.</summary>
    public static readonly TimeOnly QuietUntil = new(8, 0);

    /// <summary>The reminders from <paramref name="now"/> (local time) to the horizon, sorted by time, at most <see cref="MaxReminders"/>.</summary>
    public static IReadOnlyList<PlannedReminder> Plan(OverviewData data, ReminderSettings settings, DateTime now, int horizonDays = DefaultHorizonDays)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.Enabled)
        {
            return [];
        }

        var context = new Context(data, settings, now, now.Date.AddDays(horizonDays));
        var all = new List<PlannedReminder>();
        if (settings.Wants(ReminderKind.SubmitReadings))
        {
            all.AddRange(Readings(context));
        }

        if (settings.Wants(ReminderKind.PayBill))
        {
            all.AddRange(Bills(context));
        }

        if (settings.Wants(ReminderKind.MeterVerification))
        {
            all.AddRange(Verification(context));
        }

        if (settings.Wants(ReminderKind.DraftsToConfirm))
        {
            all.AddRange(Drafts(context));
        }

        return [.. all.OrderBy(r => r.At).ThenBy(r => r.Key, StringComparer.Ordinal).Take(MaxReminders)];
    }

    /// <summary>The time a reminder of <paramref name="day"/> is shown: the configured time, moved out of the quiet hours.</summary>
    public static DateTime TimeOn(DateOnly day, TimeOnly time)
    {
        if (time < QuietUntil)
        {
            time = QuietUntil;
        }
        else if (time >= QuietFrom)
        {
            day = day.AddDays(1);
            time = QuietUntil;
        }

        return day.ToDateTime(time);
    }

    private sealed record Context(OverviewData Data, ReminderSettings Settings, DateTime Now, DateTime Last)
    {
        public Dictionary<Guid, string> PropertyNames { get; } =
            Data.Properties.Where(p => !p.IsDeleted).ToDictionary(p => p.Id, p => p.Name);

        /// <summary>The time on <paramref name="day"/>, or null if it is not in the future or beyond the horizon.</summary>
        public DateTime? Slot(DateOnly day)
        {
            var at = TimeOn(day, Settings.Time);
            return at > Now && at.Date <= Last ? at : null;
        }
    }

    private static string Id(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static ReminderText Text(string key, params object[] args) => new(key, args);

    // ---- Readings -------------------------------------------------------------------------------------------------

    private static IEnumerable<PlannedReminder> Readings(Context context)
    {
        var data = context.Data;
        var meters = data.Meters.Where(m => !m.IsDeleted).ToDictionary(m => m.Id);
        var firstMonth = YearMonth.From(DateOnly.FromDateTime(context.Now)).AddMonths(-1);
        var groups = new Dictionary<(Guid PropertyId, DateOnly From, DateOnly To), List<Contract>>();

        foreach (var contract in data.Contracts.Where(c => !c.IsArchived && c.RemindAboutReadings && context.PropertyNames.ContainsKey(c.PropertyId)))
        {
            for (var offset = 0; offset <= 3; offset++)
            {
                if (contract.GetReadingWindow(firstMonth.AddMonths(offset)) is not { } window)
                {
                    break;
                }

                if (context.Slot(window.From) is null
                    || !contract.IsActiveOn(window.From)
                    || TodoBuilder.HasReadingInWindow(data, contract, meters, window.From, window.To))
                {
                    continue;
                }

                var key = (contract.PropertyId, window.From, window.To);
                if (!groups.TryGetValue(key, out var list))
                {
                    groups[key] = list = [];
                }

                list.Add(contract);
            }
        }

        foreach (var ((propertyId, from, to), contracts) in groups)
        {
            var titles = string.Join(", ", contracts.Select(c => c.Title).OrderBy(t => t, StringComparer.OrdinalIgnoreCase));
            yield return new PlannedReminder(
                $"readings:{Id(propertyId)}:{Day(from)}",
                context.Slot(from)!.Value,
                ReminderKind.SubmitReadings,
                Text("ReminderReadingsTitle"),
                Text("ReminderReadingsBody", context.PropertyNames[propertyId], titles, from.Day, to.Day),
                new ReminderTarget(
                    ReminderRoutes.ReadingsFor,
                    new Dictionary<string, string>
                    {
                        [ReminderRoutes.PropertyIdParameter] = Id(propertyId),
                        [ReminderRoutes.DateParameter] = Day(from),
                    }));
        }
    }

    // ---- Bills and tax installments -------------------------------------------------------------------------------

    private static IEnumerable<PlannedReminder> Bills(Context context)
    {
        var data = context.Data;
        var contracts = data.Contracts.ToDictionary(c => c.Id);
        var templates = data.BillTemplates.ToDictionary(t => t.Id);
        var payments = new Dictionary<Guid, List<Payment>>();
        foreach (var payment in data.Payments.Where(p => !p.IsDeleted))
        {
            foreach (var billId in payment.Allocations.Where(a => !a.IsDeleted).Select(a => a.BillId).Distinct())
            {
                if (!payments.TryGetValue(billId, out var list))
                {
                    payments[billId] = list = [];
                }

                list.Add(payment);
            }
        }

        var today = DateOnly.FromDateTime(context.Now);
        foreach (var bill in data.Bills.Where(b => !b.IsDeleted && !b.IsDraft && b.DueOn is not null && context.PropertyNames.ContainsKey(b.PropertyId)))
        {
            var due = bill.DueOn!.Value;
            var open = BillStatus.Of(bill, payments.GetValueOrDefault(bill.Id) ?? [], today).Open;
            if (open <= 0)
            {
                continue;
            }

            var isTax = bill.Kind == BillKind.TaxInstallment;
            var title = isTax && !string.IsNullOrWhiteSpace(bill.Title) ? bill.Title
                : bill.ContractId is { } contractId && contracts.TryGetValue(contractId, out var contract) ? contract.Title
                : bill.Title ?? string.Empty;
            var lead = bill.TemplateId is { } templateId && templates.TryGetValue(templateId, out var template) && template.RemindDaysBefore is { } own
                ? Math.Clamp(own, 0, ReminderSettings.MaxBillLeadDays)
                : context.Settings.LeadDays;
            var kind = isTax ? ReminderKind.TaxInstallment : ReminderKind.PayBill;
            var prefix = isTax ? "tax" : "bill";
            var target = new ReminderTarget(
                ReminderRoutes.BillEdit,
                new Dictionary<string, string> { [ReminderRoutes.IdParameter] = Id(bill.Id) });

            if (lead > 0 && context.Slot(due.AddDays(-lead)) is { } leadAt)
            {
                yield return new PlannedReminder(
                    $"{prefix}:{Id(bill.Id)}:lead",
                    leadAt,
                    kind,
                    DueTitle(isTax),
                    DueBody(lead, title, open, bill.Currency),
                    target);
            }

            if (context.Slot(due) is { } dueAt)
            {
                yield return new PlannedReminder($"{prefix}:{Id(bill.Id)}:due", dueAt, kind, DueTitle(isTax), DueBody(0, title, open, bill.Currency), target);
            }
        }
    }

    private static ReminderText DueTitle(bool isTax) => Text(isTax ? "ReminderTaxTitle" : "ReminderBillTitle");

    private static ReminderText DueBody(int days, string title, decimal open, Currency currency) => days switch
    {
        0 => Text("ReminderBillBodyToday", title, open, currency),
        1 => Text("ReminderBillBodyTomorrow", title, open, currency),
        _ => Text("ReminderBillBodyIn", title, open, currency, days),
    };

    // ---- Verification ---------------------------------------------------------------------------------------------

    private static IEnumerable<PlannedReminder> Verification(Context context)
    {
        foreach (var meter in context.Data.Meters.Where(m => !m.IsDeleted && m.RemovedOn is null && context.PropertyNames.ContainsKey(m.PropertyId)))
        {
            if (meter.GetVerificationDue() is not { } due)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(meter.Location) ? meter.SerialNumber ?? string.Empty : meter.Location;
            var target = new ReminderTarget(
                ReminderRoutes.MeterEdit,
                new Dictionary<string, string>
                {
                    [ReminderRoutes.IdParameter] = Id(meter.Id),
                    [ReminderRoutes.PropertyIdParameter] = Id(meter.PropertyId),
                });

            if (context.Slot(due.AddDays(-VerificationLeadDays)) is { } leadAt)
            {
                yield return new PlannedReminder(
                    $"verification:{Id(meter.Id)}:lead",
                    leadAt,
                    ReminderKind.MeterVerification,
                    Text("ReminderVerificationTitle"),
                    Text("ReminderVerificationBodyIn", name, VerificationLeadDays),
                    target);
            }

            if (context.Slot(due) is { } dueAt)
            {
                yield return new PlannedReminder(
                    $"verification:{Id(meter.Id)}:due",
                    dueAt,
                    ReminderKind.MeterVerification,
                    Text("ReminderVerificationTitle"),
                    Text("ReminderVerificationBodyToday", name),
                    target);
            }
        }
    }

    // ---- Drafts ---------------------------------------------------------------------------------------------------

    private static IEnumerable<PlannedReminder> Drafts(Context context)
    {
        var drafts = context.Data.Drafts.Where(b => !b.IsDeleted && b.IsDraft && context.PropertyNames.ContainsKey(b.PropertyId)).ToList();
        if (drafts.Count == 0)
        {
            yield break;
        }

        var properties = drafts.Select(b => b.PropertyId).Distinct().ToList();
        var parameters = new Dictionary<string, string> { [ReminderRoutes.StatusParameter] = "Drafts" };
        if (properties.Count == 1)
        {
            parameters[ReminderRoutes.PropertyIdParameter] = Id(properties[0]);
        }

        var today = DateOnly.FromDateTime(context.Now);
        var newToday = drafts.Any(b => DateOnly.FromDateTime(b.CreatedAt.ToLocalTime().DateTime) == today);

        // Every third day from the first free slot; new drafts of today that missed today's time are announced tomorrow.
        var day = today;
        if (newToday && context.Slot(today) is null)
        {
            day = today.AddDays(1);
        }

        var planned = 0;
        for (; planned < MaxDraftReminders && TimeOn(day, context.Settings.Time).Date <= context.Last; day = day.AddDays(DraftIntervalDays))
        {
            if (context.Slot(day) is not { } at)
            {
                continue;
            }

            planned++;
            yield return new PlannedReminder(
                $"drafts:{Day(day)}",
                at,
                ReminderKind.DraftsToConfirm,
                Text("ReminderDraftsTitle"),
                Text("ReminderDraftsBody", drafts.Count),
                new ReminderTarget(ReminderRoutes.Bills, parameters));
        }
    }
}
