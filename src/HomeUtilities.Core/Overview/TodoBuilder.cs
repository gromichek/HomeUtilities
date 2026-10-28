using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Overview;

/// <summary>
/// Builds the to-do list of the Overview from the data and today's date. Pure logic with fixed thresholds; the same list
/// will feed the reminders later.
/// </summary>
public static class TodoBuilder
{
    /// <summary>A reading window that opens within this many days is "soon".</summary>
    public const int ReadingsLookAheadDays = 3;

    /// <summary>A missed reading window stays on the list this many days after its last day.</summary>
    public const int ReadingsGraceDays = 3;

    /// <summary>A bill that is due within this many days is "soon".</summary>
    public const int BillLookAheadDays = 7;

    /// <summary>A meter whose verification is due within this many days is "soon" (see <see cref="MeterVerification.WarningDays"/>).</summary>
    public const int VerificationLookAheadDays = MeterVerification.WarningDays;

    /// <summary>The expected settlement of an equalized contract is listed from this absolute amount.</summary>
    public const decimal SettlementMinimum = 1.00m;

    /// <summary>The to-do items in display order: Overdue, Today, Soon, Info, then by date, then by property name.</summary>
    public static IReadOnlyList<TodoItem> Build(OverviewData data, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(data);

        var names = data.Properties.Where(p => !p.IsDeleted).ToDictionary(p => p.Id, p => p.Name);
        var items = new List<TodoItem>();
        items.AddRange(ReadingItems(data, today));
        items.AddRange(BillDueItems(data, today));
        items.AddRange(BillMissingItems(data, today));
        items.AddRange(VerificationItems(data, today));
        items.AddRange(SettlementItems(data, today));
        items.AddRange(DraftItems(data, today));

        return
        [
            .. items
                .Where(i => names.ContainsKey(i.PropertyId))
                .OrderBy(i => i.Severity)
                .ThenBy(i => i.Date)
                .ThenBy(i => names[i.PropertyId], StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Kind)
                .ThenBy(i => i.Args.Count > 0 ? i.Args[0] : string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.BillId ?? i.MeterId ?? i.ContractId ?? Guid.Empty),
        ];
    }

    // ---- Readings -------------------------------------------------------------------------------------------------

    private static IEnumerable<TodoItem> ReadingItems(OverviewData data, DateOnly today)
    {
        var current = YearMonth.From(today);
        var meters = data.Meters.Where(m => !m.IsDeleted).ToDictionary(m => m.Id);
        var groups = new Dictionary<(Guid PropertyId, DateOnly From, DateOnly To), (TodoSeverity Severity, List<Contract> Contracts)>();

        foreach (var contract in data.Contracts.Where(c => !c.IsArchived && c.IsActiveOn(today)))
        {
            for (var offset = -1; offset <= 1; offset++)
            {
                if (contract.GetReadingWindow(current.AddMonths(offset)) is not { } window)
                {
                    break;
                }

                if (SeverityOfWindow(window.From, window.To, today) is not { } severity
                    || HasReadingInWindow(data, contract, meters, window.From, window.To))
                {
                    continue;
                }

                var key = (contract.PropertyId, window.From, window.To);
                if (!groups.TryGetValue(key, out var group))
                {
                    groups[key] = group = (severity, []);
                }

                group.Contracts.Add(contract);
            }
        }

        foreach (var ((propertyId, from, to), (severity, contracts)) in groups)
        {
            var titles = contracts.Select(c => c.Title).OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
            yield return new TodoItem(
                TodoKind.ReadingsWindow,
                severity,
                from,
                propertyId,
                contracts.Count == 1 ? contracts[0].Id : null,
                null,
                null,
                null,
                titles,
                EndDate: to);
        }
    }

    private static TodoSeverity? SeverityOfWindow(DateOnly from, DateOnly to, DateOnly today)
    {
        if (from <= today && today <= to)
        {
            return TodoSeverity.Today;
        }

        if (today < from && from <= today.AddDays(ReadingsLookAheadDays))
        {
            return TodoSeverity.Soon;
        }

        return to < today && today <= to.AddDays(ReadingsGraceDays) ? TodoSeverity.Overdue : null;
    }

    /// <summary>True if any meter the contract uses in the window has a reading dated inside the window.</summary>
    private static bool HasReadingInWindow(
        OverviewData data, Contract contract, Dictionary<Guid, Meter> meters, DateOnly from, DateOnly to)
    {
        var linked = data.ContractMeters
            .Where(l => !l.IsDeleted && l.ContractId == contract.Id && l.ValidFrom <= to && (l.ValidTo is null || l.ValidTo >= from))
            .Select(l => meters.GetValueOrDefault(l.MeterId))
            .Where(m => m is not null && m.InstalledOn <= to && (m.RemovedOn is null || m.RemovedOn >= from))
            .Select(m => m!)
            .ToList();

        // A contract without a meter has nothing to read: no item, as if the readings were in.
        return linked.Count == 0
            || linked.Any(m => m.Registers.Where(r => !r.IsDeleted).SelectMany(r => r.Readings).Any(r => !r.IsDeleted && from <= r.Date && r.Date <= to));
    }

    // ---- Bills ----------------------------------------------------------------------------------------------------

    private static IEnumerable<TodoItem> BillDueItems(OverviewData data, DateOnly today)
    {
        var contracts = data.Contracts.ToDictionary(c => c.Id);
        var payments = PaymentsByBill(data.Payments);
        foreach (var bill in data.Bills.Where(b => !b.IsDeleted && !b.IsDraft && b.DueOn is not null))
        {
            var due = bill.DueOn!.Value;
            var open = BillStatus.Of(bill, payments.GetValueOrDefault(bill.Id) ?? [], today).Open;
            if (open <= 0 || due > today.AddDays(BillLookAheadDays))
            {
                continue;
            }

            var title = bill.Kind == BillKind.TaxInstallment && !string.IsNullOrWhiteSpace(bill.Title) ? bill.Title
                : bill.ContractId is { } contractId && contracts.TryGetValue(contractId, out var contract) ? contract.Title
                : bill.Title ?? string.Empty;
            yield return new TodoItem(
                bill.Kind == BillKind.TaxInstallment ? TodoKind.TaxInstallmentDue : TodoKind.BillDue,
                due < today ? TodoSeverity.Overdue : due == today ? TodoSeverity.Today : TodoSeverity.Soon,
                due,
                bill.PropertyId,
                bill.ContractId,
                null,
                bill.Id,
                open,
                [title],
                Period: bill.Period,
                Currency: bill.Currency);
        }
    }

    private static Dictionary<Guid, List<Payment>> PaymentsByBill(IEnumerable<Payment> payments)
    {
        var result = new Dictionary<Guid, List<Payment>>();
        foreach (var payment in payments.Where(p => !p.IsDeleted))
        {
            foreach (var billId in payment.Allocations.Where(a => !a.IsDeleted).Select(a => a.BillId).Distinct())
            {
                if (!result.TryGetValue(billId, out var list))
                {
                    result[billId] = list = [];
                }

                list.Add(payment);
            }
        }

        return result;
    }

    private static IEnumerable<TodoItem> BillMissingItems(OverviewData data, DateOnly today)
    {
        // A draft counts as entered: it already has its own to-do item, so the bill is not reported as missing too.
        var entered = data.Bills.Concat(data.Drafts)
            .Where(b => !b.IsDeleted && b.ContractId is not null && b.Kind is not (BillKind.Settlement or BillKind.Correction))
            .Select(b => (b.ContractId!.Value, b.Period))
            .ToHashSet();

        foreach (var contract in data.Contracts.Where(c => !c.IsArchived && c.IsActiveOn(today)))
        {
            if (contract.BillingMode == BillingMode.Assessed || contract.BillDay is not { } billDay)
            {
                continue;
            }

            var period = Bill.DefaultPeriod(contract, today);
            if (contract.ValidFrom > period.LastDay || (contract.ValidTo is { } end && end < period.FirstDay) || entered.Contains((contract.Id, period)))
            {
                continue;
            }

            var arrives = period.AddMonths(contract.BillingTiming == BillingTiming.InAdvance ? -1 : 1);
            var arrivesOn = new DateOnly(arrives.Year, arrives.Month, Math.Clamp(billDay, 1, DateTime.DaysInMonth(arrives.Year, arrives.Month)));
            if (today < arrivesOn)
            {
                continue;
            }

            yield return new TodoItem(
                TodoKind.BillMissing, TodoSeverity.Info, arrivesOn, contract.PropertyId, contract.Id, null, null, null, [contract.Title],
                Period: period, Currency: contract.Currency);
        }
    }

    // ---- Meters and settlements -----------------------------------------------------------------------------------

    private static IEnumerable<TodoItem> VerificationItems(OverviewData data, DateOnly today)
    {
        foreach (var meter in data.Meters.Where(m => !m.IsDeleted))
        {
            var status = MeterVerification.GetStatus(meter, today);
            if (status == VerificationStatus.None)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(meter.Location) ? meter.SerialNumber ?? string.Empty : meter.Location;
            yield return new TodoItem(
                TodoKind.VerificationDue,
                status == VerificationStatus.Overdue ? TodoSeverity.Overdue : TodoSeverity.Soon,
                meter.GetVerificationDue()!.Value,
                meter.PropertyId,
                null,
                meter.Id,
                null,
                null,
                [name]);
        }
    }

    private static IEnumerable<TodoItem> SettlementItems(OverviewData data, DateOnly today)
    {
        foreach (var contract in data.Contracts.Where(c => !c.IsArchived && c.IsActiveOn(today) && c.BillingMode == BillingMode.Equalized))
        {
            if (data.ExpectedSettlements.TryGetValue(contract.Id, out var expected) && Math.Abs(expected) >= SettlementMinimum)
            {
                yield return new TodoItem(
                    TodoKind.SettlementExpected, TodoSeverity.Info, today, contract.PropertyId, contract.Id, null, null, expected,
                    [contract.Title], Currency: contract.Currency);
            }
        }
    }

    private static IEnumerable<TodoItem> DraftItems(OverviewData data, DateOnly today)
    {
        foreach (var group in data.Drafts.Where(b => !b.IsDeleted && b.IsDraft).GroupBy(b => b.PropertyId))
        {
            yield return new TodoItem(
                TodoKind.DraftsToConfirm, TodoSeverity.Info, today, group.Key, null, null, null, null, [], Count: group.Count());
        }
    }
}
