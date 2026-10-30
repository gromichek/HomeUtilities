using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;
using HomeUtilities.Core.Reports;

namespace HomeUtilities.Core.Export;

/// <summary>
/// Builds the sheets of the "like my workbook" export: one sheet per contract (one row per month, laid out like the import
/// profile of the contract when there is one, in a standard layout otherwise) and a sheet with the totals per month and contract.
/// </summary>
public static partial class WorkbookExporter
{
    private const decimal PaidTolerance = 0.005m;

    private sealed class ExportRow
    {
        public YearMonth Period { get; init; }

        public DateOnly Date { get; set; }

        public bool IsExtra { get; init; }

        public Bill? Bill { get; init; }

        public Dictionary<Position, MeterReading> Readings { get; } = [];

        public string? ReplacementNote { get; init; }
    }

    private sealed record Paid(decimal Amount, DateOnly? LastDate);

    /// <summary>Builds the sheets; contracts without any bill or reading in the period get no sheet.</summary>
    public static IReadOnlyList<ExportSheet> Build(WorkbookExportInput input, WorkbookExportOptions options, ExportLabels labels)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(labels);

        var data = input.Data;
        var names = new SheetNames();
        var totalName = names.Reserve(labels.TotalSheet);
        var paid = PaidByBill(input.Payments);
        var propertyOrder = data.Properties.Select((p, i) => (p.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var suppliers = input.Suppliers.ToDictionary(s => s.Id);

        var contracts = data.Contracts
            .Where(c => options.ContractIds is null || options.ContractIds.Contains(c.Id))
            .OrderBy(c => propertyOrder.GetValueOrDefault(c.PropertyId, int.MaxValue))
            .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sheets = new List<ExportSheet>();
        foreach (var contract in contracts)
        {
            var profile = input.Profiles
                .Where(p => !p.IsDeleted && p.Profile.ContractId == contract.Id)
                .OrderByDescending(p => p.UpdatedAt)
                .FirstOrDefault()?.Profile;
            var positions = PositionsOf(data, contract, profile);
            var rows = BuildRows(data, contract, profile, positions, paid, options);
            if (rows.Count == 0)
            {
                continue;
            }

            var supplier = suppliers.TryGetValue(contract.SupplierId, out var found) ? found.Name : string.Empty;
            var name = names.Reserve($"{supplier} {contract.Title}".Trim());
            var layout = new Layout(data, contract, profile, positions, paid, options, labels);
            sheets.Add(profile is null ? layout.Standard(name, rows) : layout.Profile(name, rows));
        }

        if (TotalSheet(input, options, labels, totalName, contracts) is { } total)
        {
            sheets.Insert(0, total);
        }

        return sheets;
    }

    private static Dictionary<Guid, Paid> PaidByBill(IReadOnlyList<Payment> payments)
    {
        var result = new Dictionary<Guid, Paid>();
        foreach (var payment in payments.Where(p => !p.IsDeleted))
        {
            foreach (var allocation in payment.Allocations.Where(a => !a.IsDeleted))
            {
                result.TryGetValue(allocation.BillId, out var known);
                var last = known is null || known.LastDate is null || payment.PaidOn > known.LastDate ? payment.PaidOn : known.LastDate;
                result[allocation.BillId] = new Paid((known?.Amount ?? 0m) + allocation.Amount, last);
            }
        }

        return result;
    }

    private static List<ExportRow> BuildRows(
        ReportData data,
        Contract contract,
        ImportProfile? profile,
        List<Position> positions,
        Dictionary<Guid, Paid> paid,
        WorkbookExportOptions options)
    {
        YearMonth Resolve(DateOnly date) => ResolvePeriod(contract, profile, date);

        bool InRange(YearMonth period) => (options.From is not { } from || period >= from) && (options.To is not { } to || period <= to);

        var bills = data.Bills
            .Where(b => b.ContractId == contract.Id && !b.IsDraft && InRange(b.Period))
            .OrderBy(b => b.Period).ThenBy(b => b.Kind is BillKind.Settlement or BillKind.Correction ? 1 : 0).ThenBy(b => b.CreatedAt)
            .ToLookup(b => b.Period);
        var readings = positions.ToDictionary(
            p => p,
            p => p.Series.Select(s => s.Reading).Where(r => InRange(Resolve(r.Date))).ToLookup(r => Resolve(r.Date)));

        var periods = bills.Select(g => g.Key).Concat(readings.Values.SelectMany(l => l.Select(g => g.Key))).Distinct().OrderBy(p => p).ToList();
        var rows = new List<ExportRow>();
        foreach (var period in periods)
        {
            var periodBills = bills[period].ToList();
            var most = readings.Count == 0 ? 0 : readings.Values.Max(l => l[period].Count());
            var count = Math.Max(1, Math.Max(periodBills.Count, most));
            for (var index = 0; index < count; index++)
            {
                var row = new ExportRow { Period = period, Bill = periodBills.ElementAtOrDefault(index), IsExtra = index > 0 };
                foreach (var position in positions)
                {
                    if (readings[position][period].ElementAtOrDefault(index) is { } reading)
                    {
                        row.Readings[position] = reading;
                    }
                }

                row.Date = ChooseDate(contract, profile, period, row, paid, Resolve);
                rows.Add(row);
            }
        }

        foreach (var group in positions
            .SelectMany(p => p.Replacements.Select(r => (Position: p, Replacement: r)))
            .Where(x => InRange(Resolve(x.Replacement.Date)))
            .GroupBy(x => x.Replacement.Date))
        {
            var row = new ExportRow { Period = Resolve(group.Key), Date = group.Key, IsExtra = true, ReplacementNote = ReplacementNote(group.Key, group.OrderBy(x => x.Position.ReadingColumn).ToList()) };
            foreach (var (position, replacement) in group)
            {
                if (replacement.NewInitial is { } initial)
                {
                    row.Readings[position] = initial;
                }
            }

            rows.Add(row);
        }

        return [.. rows.OrderBy(r => r.Period).ThenBy(r => r.IsExtra ? 1 : 0).ThenBy(r => r.Date)];
    }

    // The period of a date: by the period rule of the profile, without a profile by the rule the import would suggest for the contract.
    private static YearMonth ResolvePeriod(Contract contract, ImportProfile? profile, DateOnly date)
    {
        var (rule, limit) = profile?.RuleFor(date) ?? (ImportPeriodDefaults.For(contract).Rule, ImportPeriodResolver.DefaultDayLimit);
        return ImportPeriodResolver.Resolve(date, rule, limit, contract);
    }

    // The date of a row: the first reading, else a date that the period rule turns back into the period of the row.
    private static DateOnly ChooseDate(Contract contract, ImportProfile? profile, YearMonth period, ExportRow row, Dictionary<Guid, Paid> paid, Func<DateOnly, YearMonth> resolve)
    {
        var firstReading = row.Readings.Count == 0 ? (DateOnly?)null : row.Readings.Values.Min(r => r.Date);
        var candidates = new List<DateOnly>();
        if (profile is { DateIsPaymentDate: true } && row.Bill is { } bill && IsPaid(bill, paid) && paid[bill.Id].LastDate is { } paidOn)
        {
            candidates.Add(paidOn);
        }

        if (firstReading is { } read)
        {
            candidates.Add(read);
        }

        if (contract.GetReadingWindow(period) is { } window)
        {
            candidates.Add(window.From);
        }

        candidates.AddRange([period.AddMonths(1).FirstDay, period.FirstDay, period.LastDay, period.AddMonths(-1).FirstDay, period.AddMonths(-1).LastDay, period.AddMonths(1).LastDay]);
        return candidates.FirstOrDefault(d => resolve(d) == period && profile?.OverrideFor(d) is null, period.FirstDay);
    }

    private static bool IsPaid(Bill bill, Dictionary<Guid, Paid> paid) =>
        bill.Gross != 0 && paid.TryGetValue(bill.Id, out var entry) && entry.Amount >= bill.Gross - PaidTolerance;

    // A note the importer understands again: the date, and per replaced meter its serial number, the old final and the new initial reading.
    private static string ReplacementNote(DateOnly date, IReadOnlyList<(Position Position, ReplacementRef Replacement)> items)
    {
        var parts = new List<string>();
        foreach (var (_, replacement) in items)
        {
            var word = replacement.OldMeter.Resource switch
            {
                MeterResource.ColdWater => "cold",
                MeterResource.HotWater => "hot",
                _ => null,
            };
            var serial = string.IsNullOrWhiteSpace(replacement.NewMeter.SerialNumber) ? null : replacement.NewMeter.SerialNumber.Trim();
            var values = replacement.OldFinal is { } final && replacement.NewInitial is { } initial
                ? $"reading {Number(final)} reading {Number(initial.Value)}"
                : replacement.OldFinal is { } onlyFinal ? $"reading {Number(onlyFinal)}" : null;
            var entry = word is null
                ? string.Join(' ', new[] { serial is null ? null : $"nr. {serial}", values }.Where(t => t is not null))
                : string.Join(' ', new[] { word, serial is null ? null : $"- {serial}", values }.Where(t => t is not null));
            if (entry.Length > 0)
            {
                parts.Add(entry);
            }
        }

        return $"replaced {date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}" + (parts.Count == 0 ? string.Empty : ": " + string.Join(", ", parts));
    }

    private static string Number(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Unique, valid Excel sheet names.</summary>
    private sealed class SheetNames
    {
        private readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);

        public string Reserve(string wanted)
        {
            var cleaned = Sanitize(wanted);
            var name = cleaned;
            for (var n = 2; !_used.Add(name); n++)
            {
                var suffix = $" ({n})";
                name = cleaned[..Math.Min(cleaned.Length, 31 - suffix.Length)] + suffix;
            }

            return name;
        }
    }

    /// <summary>Excel sheet names are at most 31 characters, may not contain \ / ? * [ ] : and may not start or end with an apostrophe.</summary>
    public static string Sanitize(string name)
    {
        var cleaned = new string([.. (name ?? string.Empty).Where(c => !"\\/?*[]:".Contains(c, StringComparison.Ordinal) && !char.IsControl(c))]).Trim().Trim('\'').Trim();
        if (cleaned.Length > 31)
        {
            cleaned = cleaned[..31].Trim().Trim('\'').Trim();
        }

        return cleaned.Length == 0 ? "Sheet" : cleaned;
    }
}
